using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using bot_api.Interfaces;
using bot_api.Models;

namespace bot_api;

public class WebhookProcessor
{
    private readonly ILogger<WebhookProcessor> _logger;
    private readonly IAgentChat _agentChat;

    // Deduplicate in-flight issue processing tasks
    readonly ConcurrentDictionary<string, Task> inFlightIssues = new();
    readonly string workspaceBase = "/app/workspaces";

    public WebhookProcessor(ILogger<WebhookProcessor> logger, IAgentChat agentChat)
    {
        _logger = logger;
        _agentChat = agentChat;
    }

    public async Task ProcessWebhookAsync(string eventType, string deliveryId, string rawBody, string repoName)
    {
        _logger.LogInformation($"[Processor] Processing event='{eventType}' action delivery='{deliveryId}'");
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var action = root.GetStringSafe("action") ?? string.Empty;
        _logger.LogInformation($"[Processor] repo='{repoName}' event='{eventType}' action='{action}'");

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
            EnsureRepo(localRepoPath, cloneUrl, repoName, issueNum);

            string issueKey = $"{repoName}#{issueNum}";
            if (inFlightIssues.ContainsKey(issueKey))
            {
                _logger.LogWarning($"[Processor] {issueKey} already in-flight — skipping duplicate");
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
3. Post a GitHub comment on issue #{issueNum} summarising the plan.
4. Ask for a 👍 reaction or 'approved' comment to proceed. Do NOT write any code yet.";

            _logger.LogInformation($"[Processor] Starting agent for issue #{issueNum}");
            var task = Task.Run(async () =>
            {
                (string cleanResponse, string newSessionId) = await _agentChat.GetResponseAsync(localRepoPath, issueNum, prompt);

                PostGitHubComment(localRepoPath, issueNum, cleanResponse, newSessionId);
            });
            inFlightIssues[issueKey] = task;
            try
            {
                await task;
            }
            finally
            {
                inFlightIssues.TryRemove(issueKey, out _);
                _logger.LogInformation($"[Processor] {issueKey} processing complete");
            }
            return;
        }

        // ─── REACTION EVENT: Plan approval via 👍 on the issue itself ─────────────
        if (eventType == "reaction" && action == "created")
        {
            var reactionContent = root.GetNestedStringSafe("reaction", "content") ?? string.Empty;
            _logger.LogInformation($"[Processor] Reaction event: content='{reactionContent}'");
            if (reactionContent == "+1" || reactionContent == "👍")
            {
                var issueNum = root.GetNestedStringSafe("issue", "number");
                if (string.IsNullOrEmpty(issueNum))
                {
                    _logger.LogWarning($"[Processor] reaction event missing issue.number");
                    return;
                }
                string localRepoPath = GetIssueRepoPath(repoName, issueNum);
                EnsureRepo(localRepoPath, cloneUrl, repoName, issueNum);
                string sid = GetSessionIdFromIssue(localRepoPath, issueNum);
                if (!string.IsNullOrEmpty(sid))
                    await HandleApprovalAsync(localRepoPath, issueNum, sid);
                else
                    _logger.LogWarning($"[Processor] 👍 on #{issueNum} but no active session found");
            }
            return;
        }

        // ─── ISSUE COMMENTS (FEEDBACK / APPROVAL) ────────────────────────────────
        if (eventType == "issue_comment" && action == "created")
        {
            var userType = root.GetNestedStringSafe("comment", "user", "type");
            if (userType == "Bot")
            {
                _logger.LogInformation($"[Processor] Skipping Bot comment to prevent loop");
                return;
            }

            var issueNum = root.GetNestedStringSafe("issue", "number");
            var commentBody = root.GetNestedStringSafe("comment", "body");
            if (string.IsNullOrEmpty(issueNum) || string.IsNullOrEmpty(commentBody))
            {
                _logger.LogWarning($"[Processor] issue_comment missing issue.number or comment.body");
                return;
            }

            string localRepoPath = GetIssueRepoPath(repoName, issueNum);
            EnsureRepo(localRepoPath, cloneUrl, repoName, issueNum);

            string activeSessionId = GetSessionIdFromIssue(localRepoPath, issueNum);
            _logger.LogInformation($"[Processor] issue_comment #{issueNum} activeSession='{activeSessionId}'");
            if (string.IsNullOrEmpty(activeSessionId))
            {
                _logger.LogWarning($"[Processor] No active session for #{issueNum}");
                return;
            }

            bool isApproval =
                commentBody.Trim() == "👍"
                || commentBody.Trim().Equals("lgtm", StringComparison.OrdinalIgnoreCase)
                || commentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);

            if (isApproval)
            {
                await HandleApprovalAsync(localRepoPath, issueNum, activeSessionId);
            }
            else
            {
                string execPrompt =
                    $"Feedback received on Issue #{issueNum}: '{commentBody}'. Update the plan accordingly. Post an updated plan artifact and ask for another 👍 to proceed.";
                _logger.LogInformation($"[Processor] Feedback on #{issueNum}: '{commentBody.Substring(0, Math.Min(80, commentBody.Length))}'");
                var response = await _agentChat.GetResponseAsync(localRepoPath, issueNum, execPrompt);
                PostGitHubComment(localRepoPath, issueNum, response, activeSessionId);
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
                _logger.LogInformation($"[Processor] PR merged head_ref='{headRef}'");
                var issueMatch = Regex.Match(headRef, @"fix/issue-(\d+)");
                if (issueMatch.Success)
                {
                    string issueNum = issueMatch.Groups[1].Value;
                    string path = GetIssueRepoPath(repoName, issueNum);
                    if (Directory.Exists(path))
                    {
                        _logger.LogInformation($"[Processor] Deleting isolated clone for issue #{issueNum}: '{path}'");
                        try
                        {
                            Directory.Delete(path, recursive: true);
                            _logger.LogInformation($"[Processor] Deleted '{path}'");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"[Processor] Failed to delete '{path}': {ex.Message}");
                        }
                    }
                }
            }
            return;
        }

        _logger.LogInformation($"[Processor] Unhandled event='{eventType}' action='{action}' — no-op");
        await Task.CompletedTask;
    }

    string GetIssueRepoPath(string repoName, string issueNum) => $"{workspaceBase}/{repoName}-issue-{issueNum}";

    async Task HandleApprovalAsync(string localRepoPath, string issueNum, string sessionId)
    {
        _logger.LogInformation($"[Approval] Plan approved for issue #{issueNum} (session={sessionId}) — starting implementation");
        string prompt =
            $"The plan for Issue #{issueNum} has been approved. Create branch 'fix/issue-{issueNum}', implement the code changes, run any available local tests, and raise a PR via `gh pr create`. Commit only changes related to this issue.";
        string response = await _agentChat.GetResponseAsync(localRepoPath, issueNum, prompt);
        PostGitHubComment(localRepoPath, issueNum, response, sessionId);
        _logger.LogInformation($"[Approval] Implementation complete for issue #{issueNum}");
        await Task.CompletedTask;
    }

    void EnsureRepo(string localRepoPath, string cloneUrl, string repoName, string issueNum)
    {
        if (!Directory.Exists(localRepoPath))
        {
            if (string.IsNullOrEmpty(cloneUrl))
            {
                _logger.LogWarning($"[RepoManager] No clone_url for {repoName}#{issueNum}");
                return;
            }
            _logger.LogInformation($"[RepoManager] Cloning '{cloneUrl}' -> '{localRepoPath}'");
            ExecuteProcess("git", workspaceBase, "clone", cloneUrl, localRepoPath);
        }
        else
        {
            _logger.LogInformation($"[RepoManager] Pulling latest in '{localRepoPath}'");
            ExecuteProcess("git", localRepoPath, "pull");
        }
    }

    void PostGitHubComment(string repoPath, string issueNum, string body, string sessionId)
    {
        string payload = string.IsNullOrEmpty(sessionId) ? body : $"{body}\n\n<!-- agy-session-id: {sessionId} -->";

        _logger.LogInformation($"[GitHub] Posting comment on issue #{issueNum} (session='{sessionId}', length={payload.Length})");
        ExecuteProcess("gh", repoPath, "issue", "comment", issueNum, "--body", payload);
    }

    string GetSessionIdFromIssue(string repoPath, string issueNum)
    {
        _logger.LogInformation($"[GitHub] Fetching session ID from issue #{issueNum}");
        string commentsJson = ExecuteProcess("gh", repoPath, "issue", "view", issueNum, "--json", "comments");
        var match = Regex.Match(commentsJson, @"<!-- agy-session-id: ([a-zA-Z0-9\-]+) -->", RegexOptions.RightToLeft);
        string sessionId = match.Success ? match.Groups[1].Value : string.Empty;
        _logger.LogInformation($"[GitHub] Session ID for issue #{issueNum}: '{(string.IsNullOrEmpty(sessionId) ? "none" : sessionId)}'");
        return sessionId;
    }

    string ExecuteProcess(string fileName, string workingDirectory, params string[] args)
    {
        _logger.LogInformation($"[Process] Executing: {fileName} {string.Join(" ", args)} (cwd='{workingDirectory}')");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        var sw = Stopwatch.StartNew();
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string errOut = process.StandardError.ReadToEnd();
        process.WaitForExit();
        sw.Stop();

        _logger.LogInformation($"[Process] {fileName} exited code={process.ExitCode} in {sw.Elapsed.TotalMilliseconds:F0}ms");
        if (!string.IsNullOrEmpty(errOut))
            _logger.LogWarning($"[Process] {fileName} stderr: {errOut.Trim()}");

        return output;
    }
}
