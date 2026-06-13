using System.Text.RegularExpressions;
using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workers;

public partial class WebhookProcessor
{
    private readonly ILogger<WebhookProcessor> _logger;
    private readonly IWebhookService _webhookService;
    private readonly GitHubUtils _gitHubUtils;
    private readonly IPublisher _publisher;
    private readonly IChatResolver _chatResolver;

    public WebhookProcessor(
        ILogger<WebhookProcessor> logger,
        IWebhookService webhookService,
        IChatResolver chatResolver,
        GitHubUtils gitHubUtils,
        IPublisher publisher
    )
    {
        _logger = logger;
        _webhookService = webhookService;
        _gitHubUtils = gitHubUtils;
        _publisher = publisher;
        _chatResolver = chatResolver;
    }

    internal async ValueTask ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var webhook = await _webhookService.GetNextAsync(cancellationToken);
        if (webhook is null)
        {
            return;
        }
        try
        {
            _logger.LogInformation("Processing {Webhook}", webhook);

            webhook.Status = WebhookStatus.Running;
            await _webhookService.CommitAsync(cancellationToken);

            switch (webhook)
            {
                case { EventType: "issues", Action: "opened" }:
                    await _publisher.Publish(new IssueOpened(webhook), cancellationToken);
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
            webhook.Status = WebhookStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when processing {Webhook}", webhook);
            webhook.Status = WebhookStatus.Failed;
        }
        finally
        {
            await _webhookService.CommitAsync(cancellationToken);
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
            var agentChat = _chatResolver.ResolveCurrent();
            var response = await agentChat.GetResponseAsync(
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
        var agentChat = _chatResolver.ResolveCurrent();
        string response = await agentChat.GetResponseAsync(
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
