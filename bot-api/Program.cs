using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ─── CONFIGURATION ────────────────────────────────────────────────────────────
var supportedRepos = new HashSet<string> { "antigravity-docker" };
string workspaceBase = "/app/workspaces";

// Deduplicate in-flight issue processing tasks
var inFlightIssues = new ConcurrentDictionary<string, Task>();

// ─── LOGGING HELPERS ──────────────────────────────────────────────────────────

void Log(string level, string component, string message)
{
    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z] [{level}] [{component}] {message}");
}

void LogInfo(string component, string message) => Log("INFO ", component, message);
void LogWarn(string component, string message) => Log("WARN ", component, message);
void LogError(string component, string message) => Log("ERROR", component, message);

// ─── WEBHOOK ENDPOINT ───────────────────────────────────────────────────────
// Returns 202 immediately so GitHub never times out; real work runs in background.

app.MapPost("/github-webhook", async (HttpContext context) =>
{
    var eventType  = context.Request.Headers["X-GitHub-Event"].ToString();
    var deliveryId = context.Request.Headers["X-GitHub-Delivery"].ToString();
    LogInfo("Webhook", $"Received event='{eventType}' delivery='{deliveryId}'");

    // Buffer the full body before doing anything — the stream is disposed once
    // we return the HTTP response, so the background task needs its own copy.
    string rawBody;
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        rawBody = await reader.ReadToEndAsync();
    }
    catch (Exception ex)
    {
        LogError("Webhook", $"Failed to read request body: {ex.Message}");
        return Results.BadRequest(new { error = "Failed to read body" });
    }

    JsonElement root;
    try
    {
        using var doc = JsonDocument.Parse(rawBody);
        root = doc.RootElement.Clone(); // Clone so we can use after doc is disposed
    }
    catch (JsonException ex)
    {
        LogError("Webhook", $"Invalid JSON payload: {ex.Message}");
        return Results.BadRequest(new { error = "Invalid JSON payload" });
    }

    var repoName = root.GetNestedStringSafe("repository", "name");
    if (string.IsNullOrEmpty(repoName) || !supportedRepos.Contains(repoName))
    {
        LogInfo("Webhook", $"Ignoring unsupported/missing repo: '{repoName}'");
        return Results.Ok(new { status = "Ignored: unsupported repository" });
    }

    // Fire-and-forget — GitHub gets 202 in milliseconds, no timeout risk.
    _ = Task.Run(async () =>
    {
        try { await ProcessWebhookAsync(eventType, deliveryId, rawBody, repoName); }
        catch (Exception ex) { LogError("Processor", $"Unhandled exception: {ex.Message}\n{ex.StackTrace}"); }
    });

    return Results.Accepted(value: new { status = "Accepted", delivery = deliveryId });
});

app.Run("http://0.0.0.0:8080");


// ─── BACKGROUND WEBHOOK PROCESSOR ────────────────────────────────────────────

async Task ProcessWebhookAsync(string eventType, string deliveryId, string rawBody, string repoName)
{
    LogInfo("Processor", $"Processing event='{eventType}' action delivery='{deliveryId}'");
    using var document = JsonDocument.Parse(rawBody);
    var root = document.RootElement;
    var action = root.GetStringSafe("action") ?? string.Empty;
    LogInfo("Processor", $"repo='{repoName}' event='{eventType}' action='{action}'");

    var cloneUrl = root.GetNestedStringSafe("repository", "clone_url") ?? string.Empty;

    // ─── ISSUE EVENTS ─────────────────────────────────────────────────────────
    if (eventType == "issues" && action == "opened")
    {
        var issueNum = root.GetNestedStringSafe("issue", "number");
        if (string.IsNullOrEmpty(issueNum)) { LogWarn("Processor", "issues/opened missing issue.number"); return; }

        // Each issue gets its own isolated clone so branches and commits never bleed across issues
        string localRepoPath = GetIssueRepoPath(repoName, issueNum);
        EnsureRepo(localRepoPath, cloneUrl, repoName, issueNum);

        string issueKey = $"{repoName}#{issueNum}";
        if (inFlightIssues.ContainsKey(issueKey)) { LogWarn("Processor", $"{issueKey} already in-flight — skipping duplicate"); return; }

        var title = root.GetNestedStringSafe("issue", "title") ?? string.Empty;
        var body  = root.GetNestedStringSafe("issue", "body")  ?? string.Empty;

        string prompt = $@"Analyze Issue #{issueNum}: {title}

{body}

INSTRUCTIONS:
1. Formulate a detailed implementation plan.
2. Write it to an artifact file called 'implementation_plan.md' (ArtifactType=implementation_plan). This is mandatory.
3. Post a GitHub comment on issue #{issueNum} summarising the plan.
4. Ask for a 👍 reaction or 'approved' comment to proceed. Do NOT write any code yet.";

        LogInfo("Processor", $"Starting agent for issue #{issueNum}");
        var task = Task.Run(() =>
        {
            string agentOutput  = ExecuteAgyHeadless(localRepoPath, prompt);
            string newSessionId = ExtractConversationId(agentOutput);
            LogInfo("Processor", $"Issue #{issueNum} session: '{newSessionId}'");
            string cleanResponse = GetFinalResponseFromTranscript(newSessionId);
            if (string.IsNullOrEmpty(cleanResponse)) { LogWarn("Processor", "No transcript response, using raw output"); cleanResponse = agentOutput; }

            // Embed the plan artifact content directly in the comment if available
            string planContent = TryReadPlanArtifact(newSessionId);
            if (!string.IsNullOrEmpty(planContent))
            {
                LogInfo("Processor", $"Embedding plan artifact in comment for session '{newSessionId}'");
                cleanResponse = $"{cleanResponse}\n\n---\n\n### 📋 Implementation Plan\n\n{planContent}";
            }
            else
            {
                LogWarn("Processor", $"No plan artifact found for session '{newSessionId}'");
            }
            PostGitHubComment(localRepoPath, issueNum, cleanResponse, newSessionId);
        });
        inFlightIssues[issueKey] = task;
        try   { await task; }
        finally { inFlightIssues.TryRemove(issueKey, out _); LogInfo("Processor", $"{issueKey} processing complete"); }
        return;
    }

    // ─── REACTION EVENT: Plan approval via 👍 on the issue itself ─────────────
    if (eventType == "reaction" && action == "created")
    {
        var reactionContent = root.GetNestedStringSafe("reaction", "content") ?? string.Empty;
        LogInfo("Processor", $"Reaction event: content='{reactionContent}'");
        if (reactionContent == "+1" || reactionContent == "👍")
        {
            var issueNum = root.GetNestedStringSafe("issue", "number");
            if (string.IsNullOrEmpty(issueNum)) { LogWarn("Processor", "reaction event missing issue.number"); return; }
            string localRepoPath = GetIssueRepoPath(repoName, issueNum);
            EnsureRepo(localRepoPath, cloneUrl, repoName, issueNum);
            string sid = GetSessionIdFromIssue(localRepoPath, issueNum);
            if (!string.IsNullOrEmpty(sid)) await HandleApprovalAsync(localRepoPath, issueNum, sid);
            else LogWarn("Processor", $"👍 on #{issueNum} but no active session found");
        }
        return;
    }

    // ─── ISSUE COMMENTS (FEEDBACK / APPROVAL) ────────────────────────────────
    if (eventType == "issue_comment" && action == "created")
    {
        var userType = root.GetNestedStringSafe("comment", "user", "type");
        if (userType == "Bot") { LogInfo("Processor", "Skipping Bot comment to prevent loop"); return; }

        var issueNum    = root.GetNestedStringSafe("issue", "number");
        var commentBody = root.GetNestedStringSafe("comment", "body");
        if (string.IsNullOrEmpty(issueNum) || string.IsNullOrEmpty(commentBody))
        { LogWarn("Processor", "issue_comment missing issue.number or comment.body"); return; }

        string localRepoPath = GetIssueRepoPath(repoName, issueNum);
        EnsureRepo(localRepoPath, cloneUrl, repoName, issueNum);

        string activeSessionId = GetSessionIdFromIssue(localRepoPath, issueNum);
        LogInfo("Processor", $"issue_comment #{issueNum} activeSession='{activeSessionId}'");
        if (string.IsNullOrEmpty(activeSessionId)) { LogWarn("Processor", $"No active session for #{issueNum}"); return; }

        bool isApproval = commentBody.Trim() == "👍" ||
                          commentBody.Trim().Equals("lgtm",     StringComparison.OrdinalIgnoreCase) ||
                          commentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);

        if (isApproval)
        {
            await HandleApprovalAsync(localRepoPath, issueNum, activeSessionId);
        }
        else
        {
            string execPrompt = $"Feedback received on Issue #{issueNum}: '{commentBody}'. Update the plan accordingly. Post an updated plan artifact and ask for another 👍 to proceed.";
            LogInfo("Processor", $"Feedback on #{issueNum}: '{commentBody.Substring(0, Math.Min(80, commentBody.Length))}'");
            string output   = ExecuteAgyHeadless(localRepoPath, execPrompt, activeSessionId);
            string response = GetFinalResponseFromTranscript(activeSessionId);
            if (string.IsNullOrEmpty(response)) response = output;
            PostGitHubComment(localRepoPath, issueNum, response, activeSessionId);
        }
        return;
    }

    // ─── PULL REQUEST MERGED: delete the isolated clone ───────────────────────
    if (eventType == "pull_request" && action == "closed")
    {
        bool merged = root.TryGetProperty("pull_request", out var pr) &&
                      pr.TryGetProperty("merged", out var m) && m.GetBoolean();
        if (merged)
        {
            string headRef = root.GetNestedStringSafe("pull_request", "head", "ref") ?? string.Empty;
            LogInfo("Processor", $"PR merged head_ref='{headRef}'");
            var issueMatch = Regex.Match(headRef, @"fix/issue-(\d+)");
            if (issueMatch.Success)
            {
                string issueNum = issueMatch.Groups[1].Value;
                string path     = GetIssueRepoPath(repoName, issueNum);
                if (Directory.Exists(path))
                {
                    LogInfo("Processor", $"Deleting isolated clone for issue #{issueNum}: '{path}'");
                    try   { Directory.Delete(path, recursive: true); LogInfo("Processor", $"Deleted '{path}'"); }
                    catch (Exception ex) { LogError("Processor", $"Failed to delete '{path}': {ex.Message}"); }
                }
            }
        }
        return;
    }

    LogInfo("Processor", $"Unhandled event='{eventType}' action='{action}' — no-op");
    await Task.CompletedTask;
}

async Task HandleApprovalAsync(string localRepoPath, string issueNum, string sessionId)
{
    LogInfo("Approval", $"Plan approved for issue #{issueNum} (session={sessionId}) — starting implementation");
    string prompt   = $"The plan for Issue #{issueNum} has been approved. Create branch 'fix/issue-{issueNum}', implement the code changes, run any available local tests, and raise a PR via `gh pr create`. Commit only changes related to this issue.";
    string output   = ExecuteAgyHeadless(localRepoPath, prompt, sessionId);
    string response = GetFinalResponseFromTranscript(sessionId);
    if (string.IsNullOrEmpty(response)) response = output;
    PostGitHubComment(localRepoPath, issueNum, response, sessionId);
    LogInfo("Approval", $"Implementation complete for issue #{issueNum}");
    await Task.CompletedTask;
}

string GetIssueRepoPath(string repoName, string issueNum)
    => $"{workspaceBase}/{repoName}-issue-{issueNum}";

void EnsureRepo(string localRepoPath, string cloneUrl, string repoName, string issueNum)
{
    if (!Directory.Exists(localRepoPath))
    {
        if (string.IsNullOrEmpty(cloneUrl)) { LogWarn("RepoManager", $"No clone_url for {repoName}#{issueNum}"); return; }
        LogInfo("RepoManager", $"Cloning '{cloneUrl}' -> '{localRepoPath}'");
        ExecuteProcess("git", workspaceBase, "clone", cloneUrl, localRepoPath);
    }
    else
    {
        LogInfo("RepoManager", $"Pulling latest in '{localRepoPath}'");
        ExecuteProcess("git", localRepoPath, "pull");
    }
}

string TryReadPlanArtifact(string sessionId)
{
    if (string.IsNullOrEmpty(sessionId)) return string.Empty;
    string dir = $"/root/.gemini/antigravity-cli/brain/{sessionId}";
    if (!Directory.Exists(dir)) { LogWarn("Artifact", $"Artifact dir not found: '{dir}'"); return string.Empty; }
    var candidates = Directory.GetFiles(dir, "implementation_plan.md", SearchOption.TopDirectoryOnly)
        .Concat(Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly)).ToArray();
    if (candidates.Length == 0) { LogWarn("Artifact", $"No markdown artifacts in '{dir}'"); return string.Empty; }
    try
    {
        string content = File.ReadAllText(candidates[0]);
        LogInfo("Artifact", $"Read plan artifact '{candidates[0]}': {content.Length} chars");
        return content;
    }
    catch (Exception ex) { LogError("Artifact", $"Failed reading artifact: {ex.Message}"); return string.Empty; }
}


// ─── NATIVE PROCESS EXECUTION WRAPPERS ──────────────────────────────────────

string ExecuteAgyHeadless(string repoPath, string prompt, string? conversationId = null)
{
    LogInfo("AgyRunner", $"Starting agy session='{conversationId ?? "new"}' cwd='{repoPath}'");

    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "agy",
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };

    if (!string.IsNullOrEmpty(conversationId))
    {
        process.StartInfo.ArgumentList.Add("--conversation");
        process.StartInfo.ArgumentList.Add(conversationId);
    }

    process.StartInfo.ArgumentList.Add("-p");
    process.StartInfo.ArgumentList.Add(prompt);

    var sw = Stopwatch.StartNew();
    process.Start();

    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    sw.Stop();

    LogInfo("AgyRunner", $"agy exited code={process.ExitCode} in {sw.Elapsed.TotalSeconds:F1}s");

    if (!string.IsNullOrEmpty(error))
        LogWarn("AgyRunner", $"stderr: {error.Trim()}");

    if (process.ExitCode != 0)
    {
        LogError("AgyRunner", $"agy failed: {error.Trim()}");
        return $"Error executing agent: {error}";
    }

    return output;
}

string ExecuteProcess(string fileName, string workingDirectory, params string[] args)
{
    LogInfo("Process", $"Executing: {fileName} {string.Join(" ", args)} (cwd='{workingDirectory}')");

    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };

    foreach (var arg in args)
        process.StartInfo.ArgumentList.Add(arg);

    var sw = Stopwatch.StartNew();
    process.Start();
    string output = process.StandardOutput.ReadToEnd();
    string errOut = process.StandardError.ReadToEnd();
    process.WaitForExit();
    sw.Stop();

    LogInfo("Process", $"{fileName} exited code={process.ExitCode} in {sw.Elapsed.TotalMilliseconds:F0}ms");
    if (!string.IsNullOrEmpty(errOut))
        LogWarn("Process", $"{fileName} stderr: {errOut.Trim()}");

    return output;
}

// ─── GITHUB & STATE MANAGEMENT HELPERS ──────────────────────────────────────

void PostGitHubComment(string repoPath, string issueNum, string body, string sessionId)
{
    string payload = string.IsNullOrEmpty(sessionId)
        ? body
        : $"{body}\n\n<!-- agy-session-id: {sessionId} -->";

    LogInfo("GitHub", $"Posting comment on issue #{issueNum} (session='{sessionId}', length={payload.Length})");
    ExecuteProcess("gh", repoPath, "issue", "comment", issueNum, "--body", payload);
}

string GetSessionIdFromIssue(string repoPath, string issueNum)
{
    LogInfo("GitHub", $"Fetching session ID from issue #{issueNum}");
    string commentsJson = ExecuteProcess("gh", repoPath, "issue", "view", issueNum, "--json", "comments");
    var match = Regex.Match(commentsJson, @"<!-- agy-session-id: ([a-zA-Z0-9\-]+) -->", RegexOptions.RightToLeft);
    string sessionId = match.Success ? match.Groups[1].Value : string.Empty;
    LogInfo("GitHub", $"Session ID for issue #{issueNum}: '{(string.IsNullOrEmpty(sessionId) ? "none" : sessionId)}'");
    return sessionId;
}

string ExtractConversationId(string agyOutput)
{
    var match = Regex.Match(agyOutput, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
    string id = match.Success ? match.Value : string.Empty;
    LogInfo("AgyRunner", $"Extracted conversation ID: '{(string.IsNullOrEmpty(id) ? "none found" : id)}'");
    return id;
}

string GetFinalResponseFromTranscript(string sessionId)
{
    if (string.IsNullOrEmpty(sessionId)) return string.Empty;

    var transcriptPath = $"/root/.gemini/antigravity-cli/brain/{sessionId}/.system_generated/logs/transcript.jsonl";
    LogInfo("Transcript", $"Reading transcript for session '{sessionId}'");

    if (!File.Exists(transcriptPath))
    {
        LogWarn("Transcript", $"Transcript not found at '{transcriptPath}'");
        return string.Empty;
    }

    string finalContent = string.Empty;
    int linesRead = 0, responsesFound = 0;
    try
    {
        foreach (var line in File.ReadLines(transcriptPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            linesRead++;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "PLANNER_RESPONSE")
                {
                    if (root.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                    {
                        var content = contentProp.GetString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            finalContent = content;
                            responsesFound++;
                        }
                    }
                }
            }
            catch { /* Ignore malformed transcript lines */ }
        }
    }
    catch (Exception ex)
    {
        LogError("Transcript", $"Error reading transcript for session '{sessionId}': {ex.Message}");
    }

    LogInfo("Transcript", $"Read {linesRead} lines, {responsesFound} planner responses (session='{sessionId}')");
    return finalContent;
}

public static class JsonElementExtensions
{
    public static string? GetStringSafe(this JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var prop))
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        return null;
    }

    public static string? GetNestedStringSafe(this JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
                return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}