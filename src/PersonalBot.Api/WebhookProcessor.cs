using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Models;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Utils;

namespace PersonalBot.Api;

public partial class WebhookProcessor
{
    private readonly ILogger<WebhookProcessor> _logger;
    private readonly GitHubUtils _gitHubUtils;
    private readonly IAgentChat _agentChat;

    // Deduplicate in-flight issue processing tasks
    readonly ConcurrentDictionary<string, Task> _inFlightIssues = new();

    public WebhookProcessor(ILogger<WebhookProcessor> logger, IChatResolver chatResolver, GitHubUtils gitHubUtils)
    {
        _logger = logger;
        _gitHubUtils = gitHubUtils;
        _agentChat = chatResolver.ResolveCurrent();
    }

    public async ValueTask ProcessWebhookAsync(GitHubWebhook webhook, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing event='{eventType}' action delivery='{deliveryId}' repo='{repoName}' action='{action}'",
            webhook.EventType,
            webhook.DeliveryId,
            webhook.RepoName,
            webhook.Action
        );

        switch (webhook)
        {
            case { EventType: "issues", Action: "opened" }:
                await HandleIssueOpened(webhook, cancellationToken);
                break;
            case { EventType: "reaction", Action: "created" }:
                await HandleReactionCreated(webhook, cancellationToken);
                break;
            case { EventType: "issue_comment", Action: "created" }:
                await HandleIssueCommentCreated(webhook, cancellationToken);
                break;
            case { EventType: "pull_request", Action: "closed" }:
                HandlePullRequestClosed(webhook);
                break;
            default:
                _logger.LogInformation("[Processor] Unhandled event='{eventType}' action='{action}' — no-op", webhook.EventType, webhook.Action);
                break;
        }
    }

    private async Task HandleIssueOpened(GitHubWebhook webhook, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(webhook.IssueNumber))
        {
            _logger.LogWarning($"issues/opened missing issue.number");
            return;
        }

        // Each issue gets its own isolated clone so branches and commits never bleed across issues
        string localRepoPath = GitHubUtils.GetIssueRepoPath(webhook.RepoName, webhook.IssueNumber);
        await _gitHubUtils.EnsureRepoAsync(localRepoPath, webhook.CloneUrl, webhook.RepoName, webhook.IssueNumber, cancellationToken);

        string issueKey = $"{webhook.RepoName}#{webhook.IssueNumber}";
        if (_inFlightIssues.ContainsKey(issueKey))
        {
            _logger.LogWarning("[Processor] {issueKey} already in-flight — skipping duplicate", issueKey);
            return;
        }

        var title = webhook.IssueTitle ?? string.Empty;
        var body = webhook.IssueBody ?? string.Empty;

        string prompt =
            $@"Analyze Issue #{webhook.IssueNumber}: {title}

{body}

INSTRUCTIONS:
1. Formulate a detailed implementation plan.
2. Write it to an artifact file called 'implementation_plan.md' (ArtifactType=implementation_plan). This is mandatory.
3. Answer with a GitHub comment for issue #{webhook.IssueNumber} summarising the plan.
4. Ask for a 👍 reaction or 'approved' comment to proceed. Do NOT write any code yet.";

        _logger.LogInformation("[Processor] Starting agent for issue #{issueNum}", webhook.IssueNumber);
        var task = Task.Run(
            async () =>
            {
                (string cleanResponse, string newSessionId, string? artifactOutput) = await _agentChat.GetResponseAsync(
                    new()
                    {
                        RepoPath = localRepoPath,
                        IssueNum = webhook.IssueNumber,
                        Prompt = prompt,
                        Phase = AgentPhase.Planning,
                    },
                    cancellationToken
                );
                var comment = !string.IsNullOrEmpty(artifactOutput) ? artifactOutput : cleanResponse;
                await _gitHubUtils.PostGitHubCommentAsync(localRepoPath, webhook.IssueNumber, comment, newSessionId, cancellationToken);
            },
            CancellationToken.None
        );
        _inFlightIssues[issueKey] = task;
        try
        {
            await task;
        }
        finally
        {
            _inFlightIssues.TryRemove(issueKey, out _);
            _logger.LogInformation("[Processor] {issueKey} processing complete", issueKey);
        }
    }

    private async Task HandleReactionCreated(GitHubWebhook webhook, CancellationToken cancellationToken)
    {
        var reactionContent = webhook.ReactionContent;
        _logger.LogInformation("[Processor] Reaction event: content='{reactionContent}'", reactionContent);
        if (reactionContent == "+1" || reactionContent == "👍")
        {
            if (string.IsNullOrEmpty(webhook.IssueNumber))
            {
                _logger.LogWarning($"[Processor] reaction event missing issue.number");
                return;
            }
            string localRepoPath = GitHubUtils.GetIssueRepoPath(webhook.RepoName, webhook.IssueNumber);
            await _gitHubUtils.EnsureRepoAsync(localRepoPath, webhook.CloneUrl, webhook.RepoName, webhook.IssueNumber, cancellationToken);
            string sid = await _gitHubUtils.GetSessionIdFromIssueAsync(localRepoPath, webhook.IssueNumber, cancellationToken);
            if (!string.IsNullOrEmpty(sid))
                await HandleApprovalAsync(localRepoPath, webhook.IssueNumber, sid, cancellationToken);
            else
                _logger.LogWarning("[Processor] 👍 on #{issueNum} but no active session found", webhook.IssueNumber);
        }
    }

    private async Task HandleIssueCommentCreated(GitHubWebhook webhook, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(webhook.IssueNumber) || string.IsNullOrEmpty(webhook.CommentBody))
        {
            _logger.LogWarning($"[Processor] issue_comment missing issue.number or comment.body");
            return;
        }

        if (GitHubUtils.IsOwnComment(webhook.CommentBody))
        {
            _logger.LogInformation($"[Processor] Skipping Bot comment to prevent loop");
            return;
        }

        string localRepoPath = GitHubUtils.GetIssueRepoPath(webhook.RepoName, webhook.IssueNumber);
        await _gitHubUtils.EnsureRepoAsync(localRepoPath, webhook.CloneUrl, webhook.RepoName, webhook.IssueNumber, cancellationToken);

        string activeSessionId = await _gitHubUtils.GetSessionIdFromIssueAsync(localRepoPath, webhook.IssueNumber, cancellationToken);
        _logger.LogInformation("[Processor] issue_comment #{issueNum} activeSession='{activeSessionId}'", webhook.IssueNumber, activeSessionId);
        if (string.IsNullOrEmpty(activeSessionId))
        {
            _logger.LogWarning("[Processor] No active session for #{issueNum}", webhook.IssueNumber);
            return;
        }

        bool isApproval =
            webhook.CommentBody.Trim() == "👍"
            || webhook.CommentBody.Trim().Equals("lgtm", StringComparison.OrdinalIgnoreCase)
            || webhook.CommentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);

        if (isApproval)
        {
            await HandleApprovalAsync(localRepoPath, webhook.IssueNumber, activeSessionId, cancellationToken);
        }
        else
        {
            string execPrompt =
                $"Feedback received on Issue #{webhook.IssueNumber}: '{webhook.CommentBody}'. Update the plan accordingly. Post an updated plan artifact and ask for another 👍 to proceed.";
            _logger.LogInformation(
                "[Processor] Feedback on #{issueNum}: '{CommentBody}'",
                webhook.IssueNumber,
                webhook.CommentBody[..Math.Min(80, webhook.CommentBody.Length)]
            );
            var response = await _agentChat.GetResponseAsync(
                new()
                {
                    RepoPath = localRepoPath,
                    IssueNum = webhook.IssueNumber,
                    Prompt = execPrompt,
                    Phase = AgentPhase.Planning,
                },
                cancellationToken
            );
            var comment = !string.IsNullOrEmpty(response.ArtifactOutput) ? response.ArtifactOutput : response.Output;
            await _gitHubUtils.PostGitHubCommentAsync(localRepoPath, webhook.IssueNumber, comment, activeSessionId, cancellationToken);
        }
    }

    private void HandlePullRequestClosed(GitHubWebhook webhook)
    {
        if (webhook.PrMerged)
        {
            _logger.LogInformation("[Processor] PR merged head_ref='{headRef}'", webhook.HeadRef);
            var issueMatch = IssueRegex.Match(webhook.HeadRef ?? string.Empty);
            if (issueMatch.Success)
            {
                string issueNum = issueMatch.Groups[1].Value;
                string path = GitHubUtils.GetIssueRepoPath(webhook.RepoName, issueNum);
                if (Directory.Exists(path))
                {
                    _logger.LogInformation("[Processor] Deleting isolated clone for issue #{issueNum}: '{path}'", issueNum, path);
                    try
                    {
                        Directory.Delete(path, recursive: true);
                        _logger.LogInformation("[Processor] Deleted '{path}'", path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Processor] Failed to delete '{Path}': {ExceptionMessage}", path, ex.Message);
                    }
                }
            }
        }
    }

    private async Task HandleApprovalAsync(string localRepoPath, string issueNum, string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Approval] Plan approved for issue #{issueNum} (session={sessionId}) — starting implementation", issueNum, sessionId);
        string prompt =
            $"The plan for Issue #{issueNum} has been approved. Create branch 'fix/issue-{issueNum}', implement the code changes, run any available local tests, and raise a PR via `gh pr create`. Commit only changes related to this issue.";
        string response = await _agentChat.GetResponseAsync(
            new()
            {
                RepoPath = localRepoPath,
                IssueNum = issueNum,
                Prompt = prompt,
                Phase = AgentPhase.Execution,
            },
            cancellationToken
        );
        await _gitHubUtils.PostGitHubCommentAsync(localRepoPath, issueNum, response, sessionId, cancellationToken);
        _logger.LogInformation("[Approval] Implementation complete for issue #{issueNum}", issueNum);
    }

    [GeneratedRegex(@"fix/issue-(\d+)")]
    private static partial Regex IssueRegex { get; }
}
