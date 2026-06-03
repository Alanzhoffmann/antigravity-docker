using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ─── CONFIGURATION ────────────────────────────────────────────────────────────
var supportedRepos = new HashSet<string> { "antigravity-docker" };
string workspaceBase = "/app/workspaces";

// ─── LOGGING HELPERS ──────────────────────────────────────────────────────────

void Log(string level, string component, string message)
{
    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z] [{level}] [{component}] {message}");
}

void LogInfo(string component, string message) => Log("INFO ", component, message);
void LogWarn(string component, string message) => Log("WARN ", component, message);
void LogError(string component, string message) => Log("ERROR", component, message);

// ─── WEBHOOK ENDPOINT ─────────────────────────────────────────────────────────

app.MapPost("/github-webhook", async (HttpContext context) =>
{
    try
    {
        var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
        var deliveryId = context.Request.Headers["X-GitHub-Delivery"].ToString();
        LogInfo("Webhook", $"Received event='{eventType}' delivery='{deliveryId}'");

        using var document = await JsonDocument.ParseAsync(context.Request.Body);
        var root = document.RootElement;

        var repoName = root.GetNestedStringSafe("repository", "name");
        if (string.IsNullOrEmpty(repoName) || !supportedRepos.Contains(repoName))
        {
            LogInfo("Webhook", $"Ignoring unsupported/missing repo: '{repoName}'");
            return Results.Ok(new { status = "Ignored: Missing or unsupported repository" });
        }

        var localRepoPath = $"{workspaceBase}/{repoName}";
        var action = root.GetStringSafe("action");
        LogInfo("Webhook", $"repo='{repoName}' action='{action}'");

        // Ensure repo exists locally
        if (!Directory.Exists(localRepoPath))
        {
            var cloneUrl = root.GetNestedStringSafe("repository", "clone_url");
            if (string.IsNullOrEmpty(cloneUrl))
            {
                LogWarn("Webhook", "Missing clone_url in repository payload.");
                return Results.BadRequest(new { error = "Missing clone_url" });
            }
            LogInfo("Webhook", $"Cloning '{cloneUrl}' -> '{localRepoPath}'");
            ExecuteProcess("git", workspaceBase, "clone", cloneUrl, localRepoPath);
        }
        else
        {
            LogInfo("Webhook", $"Repo exists at '{localRepoPath}', pulling latest");
            ExecuteProcess("git", localRepoPath, "pull");
        }

        // ─── ISSUE EVENTS (PLANNING & EXECUTION) ─────────────────────────────────
        if (eventType == "issues")
        {
            var issueNum = root.GetNestedStringSafe("issue", "number");
            if (string.IsNullOrEmpty(issueNum))
            {
                LogWarn("Webhook", "Received 'issues' event but 'issue.number' is missing.");
                return Results.Ok(new { status = "Ignored: Missing issue number" });
            }

            if (action == "opened")
            {
                var title = root.GetNestedStringSafe("issue", "title") ?? string.Empty;
                var body = root.GetNestedStringSafe("issue", "body") ?? string.Empty;

                string prompt = $"Analyze Issue #{issueNum}: {title}. {body}. Do NOT write code or create/modify any files/artifacts yet (do NOT write implementation_plan.md or any other markdown files). Formulate an implementation plan in your standard text response only. End by asking for a 👍 reaction to execute.";

                LogInfo("Webhook", $"Starting agent for issue #{issueNum}");
                string agentOutput = ExecuteAgyHeadless(localRepoPath, prompt);

                string newSessionId = ExtractConversationId(agentOutput);
                LogInfo("Webhook", $"Issue #{issueNum} session: '{newSessionId}'");

                string cleanResponse = GetFinalResponseFromTranscript(newSessionId);
                if (string.IsNullOrEmpty(cleanResponse))
                {
                    LogWarn("Webhook", $"No transcript response found for session '{newSessionId}', using raw output");
                    cleanResponse = agentOutput;
                }
                PostGitHubComment(localRepoPath, issueNum, cleanResponse, newSessionId);
            }
            else if (action == "edited" || action == "labeled")
            {
                string activeSessionId = GetSessionIdFromIssue(localRepoPath, issueNum);
                LogInfo("Webhook", $"Issue #{issueNum} action='{action}' activeSession='{activeSessionId}'");

                if (!string.IsNullOrEmpty(activeSessionId))
                {
                    string prompt = $"The plan for Issue #{issueNum} is approved. Create branch 'fix/issue-{issueNum}', implement the code, run local tests, and raise a PR via `gh pr create`.";
                    ExecuteAgyHeadless(localRepoPath, prompt, activeSessionId);
                }
            }
        }

        // ─── ISSUE COMMENTS (FEEDBACK LOOP) ──────────────────────────────────────
        else if (eventType == "issue_comment" && action == "created")
        {
            var userType = root.GetNestedStringSafe("comment", "user", "type");

            if (userType != "Bot") // Prevent infinite bot-looping
            {
                var issueNum = root.GetNestedStringSafe("issue", "number");
                var commentBody = root.GetNestedStringSafe("comment", "body");

                if (string.IsNullOrEmpty(issueNum) || string.IsNullOrEmpty(commentBody))
                {
                    LogWarn("Webhook", "Received 'issue_comment' event but 'issue.number' or 'comment.body' is missing.");
                    return Results.Ok(new { status = "Ignored: Missing issue or comment data" });
                }

                string activeSessionId = GetSessionIdFromIssue(localRepoPath, issueNum);
                LogInfo("Webhook", $"issue_comment on #{issueNum} activeSession='{activeSessionId}'");

                if (!string.IsNullOrEmpty(activeSessionId))
                {
                    bool isApproval = commentBody.Trim() == "👍" ||
                                       commentBody.Trim().Equals("lgtm", StringComparison.OrdinalIgnoreCase) ||
                                       commentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);

                    string prompt;
                    if (isApproval)
                    {
                        LogInfo("Webhook", $"Approval detected for issue #{issueNum}");
                        prompt = $"The plan for Issue #{issueNum} is approved. Create branch 'fix/issue-{issueNum}', implement the code, run local tests, and raise a PR via `gh pr create`.";
                    }
                    else
                    {
                        LogInfo("Webhook", $"Feedback comment on issue #{issueNum}: '{commentBody.Substring(0, Math.Min(80, commentBody.Length))}'");
                        prompt = $"Feedback received: '{commentBody}'. Please update the plan or code accordingly.";
                    }

                    string agentOutput = ExecuteAgyHeadless(localRepoPath, prompt, activeSessionId);
                    string cleanResponse = GetFinalResponseFromTranscript(activeSessionId);
                    if (string.IsNullOrEmpty(cleanResponse))
                    {
                        cleanResponse = agentOutput;
                    }

                    PostGitHubComment(localRepoPath, issueNum, cleanResponse, activeSessionId);
                }
                else
                {
                    LogWarn("Webhook", $"No active session found for issue #{issueNum}, ignoring comment");
                }
            }
            else
            {
                LogInfo("Webhook", "Skipping Bot comment to prevent loop");
            }
        }
        else
        {
            LogInfo("Webhook", $"Unhandled event='{eventType}' action='{action}' — no-op");
        }

        return Results.Ok(new { status = "Success" });
    }
    catch (JsonException ex)
    {
        LogError("Webhook", $"Invalid JSON payload: {ex.Message}");
        return Results.BadRequest(new { error = "Invalid JSON payload" });
    }
    catch (Exception ex)
    {
        LogError("Webhook", $"Unexpected error: {ex.Message}\n{ex.StackTrace}");
        return Results.Ok(new { status = "Error", message = "An unexpected error occurred" });
    }
});

app.Run("http://0.0.0.0:8080");


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