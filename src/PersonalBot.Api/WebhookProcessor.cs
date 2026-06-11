using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Utils;

namespace PersonalBot.Api;

public partial class WebhookProcessor
{
    private readonly ILogger<WebhookProcessor> _logger;
    private readonly IAgentChat _agentChat;
    private readonly ProcessUtils _processUtils;
    private const string BotWatermark = "<!-- from-bot: true -->";

    // Deduplicate in-flight issue processing tasks
    readonly ConcurrentDictionary<string, Task> inFlightIssues = new();
    readonly string workspaceBase = "/app/workspaces";

    public WebhookProcessor(ILogger<WebhookProcessor> logger, IChatResolver chatResolver, ProcessUtils processUtils)
    {
        _logger = logger;
        _agentChat = chatResolver.ResolveCurrent();

        _processUtils = processUtils;
    }

    public async ValueTask ProcessWebhookAsync(
        string eventType,
        string deliveryId,
        string rawBody,
        string repoName,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("[Processor] Processing event='{eventType}' action delivery='{deliveryId}'", eventType, deliveryId);
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var action = root.GetStringSafe("action") ?? string.Empty;
        _logger.LogInformation("[Processor] repo='{repoName}' event='{eventType}' action='{action}'", repoName, eventType, action);

        var cloneUrl = root.GetNestedStringSafe("repository", "clone_url") ?? string.Empty;

        // ─── ISSUE EVENTS ─────────────────────────────────────────────────────────
        if (eventType == "issues" && action == "opened")
        {
            var issueNum = root.GetNestedStringSafe("issue", "number");
            if (string.IsNullOrEmpty(issueNum))
            {
                _logger.LogWarning($"[Processor] issues/opened missing issue.number");
                return;
            }

            // Each issue gets its own isolated clone so branches and commits never bleed across issues
            string localRepoPath = GetIssueRepoPath(repoName, issueNum);
            await EnsureRepoAsync(localRepoPath, cloneUrl, repoName, issueNum, cancellationToken);

            string issueKey = $"{repoName}#{issueNum}";
            if (inFlightIssues.ContainsKey(issueKey))
            {
                _logger.LogWarning("[Processor] {issueKey} already in-flight — skipping duplicate", issueKey);
                return;
            }

            var title = root.GetNestedStringSafe("issue", "title") ?? string.Empty;
            var body = root.GetNestedStringSafe("issue", "body") ?? string.Empty;

            string prompt =
                $@"Analyze Issue #{issueNum}: {title}

{body}

INSTRUCTIONS:
1. Formulate a detailed implementation plan.
2. Write it to an artifact file called 'implementation_plan.md' (ArtifactType=implementation_plan). This is mandatory.
3. Answer with a GitHub comment for issue #{issueNum} summarising the plan.
4. Ask for a 👍 reaction or 'approved' comment to proceed. Do NOT write any code yet.";

            _logger.LogInformation("[Processor] Starting agent for issue #{issueNum}", issueNum);
            var task = Task.Run(
                async () =>
                {
                    (string cleanResponse, string newSessionId, string? artifactOutput) = await _agentChat.GetResponseAsync(
                        localRepoPath,
                        issueNum,
                        prompt,
                        cancellationToken
                    );
                    var comment = !string.IsNullOrEmpty(artifactOutput) ? artifactOutput : cleanResponse;
                    await PostGitHubCommentAsync(localRepoPath, issueNum, comment, newSessionId, cancellationToken);
                },
                CancellationToken.None
            );
            inFlightIssues[issueKey] = task;
            try
            {
                await task;
            }
            finally
            {
                inFlightIssues.TryRemove(issueKey, out _);
                _logger.LogInformation("[Processor] {issueKey} processing complete", issueKey);
            }
            return;
        }

        // ─── REACTION EVENT: Plan approval via 👍 on the issue itself ─────────────
        if (eventType == "reaction" && action == "created")
        {
            var reactionContent = root.GetNestedStringSafe("reaction", "content") ?? string.Empty;
            _logger.LogInformation("[Processor] Reaction event: content='{reactionContent}'", reactionContent);
            if (reactionContent == "+1" || reactionContent == "👍")
            {
                var issueNum = root.GetNestedStringSafe("issue", "number");
                if (string.IsNullOrEmpty(issueNum))
                {
                    _logger.LogWarning($"[Processor] reaction event missing issue.number");
                    return;
                }
                string localRepoPath = GetIssueRepoPath(repoName, issueNum);
                await EnsureRepoAsync(localRepoPath, cloneUrl, repoName, issueNum, cancellationToken);
                string sid = await GetSessionIdFromIssueAsync(localRepoPath, issueNum, cancellationToken);
                if (!string.IsNullOrEmpty(sid))
                    await HandleApprovalAsync(localRepoPath, issueNum, sid, cancellationToken);
                else
                    _logger.LogWarning("[Processor] 👍 on #{issueNum} but no active session found", issueNum);
            }
            return;
        }

        // ─── ISSUE COMMENTS (FEEDBACK / APPROVAL) ────────────────────────────────
        if (eventType == "issue_comment" && action == "created")
        {
            var issueNum = root.GetNestedStringSafe("issue", "number");
            var commentBody = root.GetNestedStringSafe("comment", "body");
            if (string.IsNullOrEmpty(issueNum) || string.IsNullOrEmpty(commentBody))
            {
                _logger.LogWarning($"[Processor] issue_comment missing issue.number or comment.body");
                return;
            }

            if (IsOwnComment(commentBody))
            {
                _logger.LogInformation($"[Processor] Skipping Bot comment to prevent loop");
                return;
            }

            string localRepoPath = GetIssueRepoPath(repoName, issueNum);
            await EnsureRepoAsync(localRepoPath, cloneUrl, repoName, issueNum, cancellationToken);

            string activeSessionId = await GetSessionIdFromIssueAsync(localRepoPath, issueNum, cancellationToken);
            _logger.LogInformation("[Processor] issue_comment #{issueNum} activeSession='{activeSessionId}'", issueNum, activeSessionId);
            if (string.IsNullOrEmpty(activeSessionId))
            {
                _logger.LogWarning("[Processor] No active session for #{issueNum}", issueNum);
                return;
            }

            bool isApproval =
                commentBody.Trim() == "👍"
                || commentBody.Trim().Equals("lgtm", StringComparison.OrdinalIgnoreCase)
                || commentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);

            if (isApproval)
            {
                await HandleApprovalAsync(localRepoPath, issueNum, activeSessionId, cancellationToken);
            }
            else
            {
                string execPrompt =
                    $"Feedback received on Issue #{issueNum}: '{commentBody}'. Update the plan accordingly. Post an updated plan artifact and ask for another 👍 to proceed.";
                _logger.LogInformation("[Processor] Feedback on #{issueNum}: '{CommentBody}'", issueNum, commentBody[..Math.Min(80, commentBody.Length)]);
                var response = await _agentChat.GetResponseAsync(localRepoPath, issueNum, execPrompt, cancellationToken);
                var comment = !string.IsNullOrEmpty(response.ArtifactOutput) ? response.ArtifactOutput : response.Output;
                await PostGitHubCommentAsync(localRepoPath, issueNum, comment, activeSessionId, cancellationToken);
            }
            return;
        }

        // ─── PULL REQUEST MERGED: delete the isolated clone ───────────────────────
        if (eventType == "pull_request" && action == "closed")
        {
            bool merged = root.TryGetProperty("pull_request", out var pr) && pr.TryGetProperty("merged", out var m) && m.GetBoolean();
            if (merged)
            {
                string headRef = root.GetNestedStringSafe("pull_request", "head", "ref") ?? string.Empty;
                _logger.LogInformation("[Processor] PR merged head_ref='{headRef}'", headRef);
                var issueMatch = IssueRegex.Match(headRef);
                if (issueMatch.Success)
                {
                    string issueNum = issueMatch.Groups[1].Value;
                    string path = GetIssueRepoPath(repoName, issueNum);
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
            return;
        }

        _logger.LogInformation("[Processor] Unhandled event='{eventType}' action='{action}' — no-op", eventType, action);
        await ValueTask.CompletedTask;
    }

    string GetIssueRepoPath(string repoName, string issueNum) => $"{workspaceBase}/{repoName}-issue-{issueNum}";

    async Task HandleApprovalAsync(string localRepoPath, string issueNum, string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Approval] Plan approved for issue #{issueNum} (session={sessionId}) — starting implementation", issueNum, sessionId);
        string prompt =
            $"The plan for Issue #{issueNum} has been approved. Create branch 'fix/issue-{issueNum}', implement the code changes, run any available local tests, and raise a PR via `gh pr create`. Commit only changes related to this issue.";
        string response = await _agentChat.GetResponseAsync(localRepoPath, issueNum, prompt, cancellationToken);
        await PostGitHubCommentAsync(localRepoPath, issueNum, response, sessionId, cancellationToken);
        _logger.LogInformation("[Approval] Implementation complete for issue #{issueNum}", issueNum);
    }

    async Task EnsureRepoAsync(string localRepoPath, string cloneUrl, string repoName, string issueNum, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(localRepoPath))
        {
            if (string.IsNullOrEmpty(cloneUrl))
            {
                _logger.LogWarning("[RepoManager] No clone_url for {repoName}#{issueNum}", repoName, issueNum);
                return;
            }
            _logger.LogInformation("[RepoManager] Cloning '{cloneUrl}' -> '{localRepoPath}'", cloneUrl, localRepoPath);
            await _processUtils.RunProcessAsync("git", ["clone", cloneUrl, localRepoPath], workspaceBase, cancellationToken);
        }
        else
        {
            _logger.LogInformation("[RepoManager] Pulling latest in '{localRepoPath}'", localRepoPath);
            await _processUtils.RunProcessAsync("git", ["pull"], localRepoPath, cancellationToken);
        }
    }

    async Task PostGitHubCommentAsync(string repoPath, string issueNum, string body, string sessionId, CancellationToken cancellationToken = default)
    {
        string payload = string.IsNullOrEmpty(sessionId) ? body : $"{body}\n\n<!-- agy-session-id: {sessionId} -->";

        payload += $"\n\n{BotWatermark}";

        _logger.LogInformation(
            "[GitHub] Posting comment on issue #{issueNum} (session='{sessionId}', length={PayloadLength})",
            issueNum,
            sessionId,
            payload.Length
        );
        await _processUtils.RunProcessAsync("gh", ["issue", "comment", issueNum, "--body", payload], repoPath, cancellationToken);
    }

    async Task<string> GetSessionIdFromIssueAsync(string repoPath, string issueNum, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[GitHub] Fetching session ID from issue #{issueNum}", issueNum);
        string commentsJson = await _processUtils.RunProcessAsync("gh", ["issue", "view", issueNum, "--json", "comments"], repoPath, cancellationToken);
        var match = SessionIdRegex.Match(commentsJson);
        string sessionId = match.Success ? match.Groups[1].Value : string.Empty;
        _logger.LogInformation("[GitHub] Session ID for issue #{issueNum}: '{sessionId}'", issueNum, string.IsNullOrEmpty(sessionId) ? "none" : sessionId);
        return sessionId;
    }

    static bool IsOwnComment(string commentBody) => commentBody.Contains(BotWatermark);

    [GeneratedRegex(@"fix/issue-(\d+)")]
    private static partial Regex IssueRegex { get; }

    [GeneratedRegex(@"<!-- agy-session-id: ([a-zA-Z0-9\-]+) -->", RegexOptions.RightToLeft)]
    private static partial Regex SessionIdRegex { get; }
}
