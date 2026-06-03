using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Configuration
var supportedRepos = new HashSet<string> { "antigravity-docker" };
string workspaceBase = "/app/workspaces";

app.MapPost("/github-webhook", async (HttpContext context) =>
{
    try
    {
        var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
        using var document = await JsonDocument.ParseAsync(context.Request.Body);
        var root = document.RootElement;
        
        var repoName = root.GetNestedStringSafe("repository", "name");
        if (string.IsNullOrEmpty(repoName) || !supportedRepos.Contains(repoName))
        {
            return Results.Ok(new { status = "Ignored: Missing or unsupported repository" });
        }

        var localRepoPath = $"{workspaceBase}/{repoName}";
        var action = root.GetStringSafe("action");

        // Ensure repo exists locally
        if (!Directory.Exists(localRepoPath))
        {
            var cloneUrl = root.GetNestedStringSafe("repository", "clone_url");
            if (string.IsNullOrEmpty(cloneUrl))
            {
                Console.WriteLine("[Warning] Missing clone_url in repository payload.");
                return Results.BadRequest(new { error = "Missing clone_url" });
            }
            ExecuteProcess("git", workspaceBase, "clone", cloneUrl, localRepoPath);
        }
        else
        {
            ExecuteProcess("git", localRepoPath, "pull");
        }

        // ─── ISSUE EVENTS (PLANNING & EXECUTION) ─────────────────────────────────
        if (eventType == "issues")
        {
            var issueNum = root.GetNestedStringSafe("issue", "number");
            if (string.IsNullOrEmpty(issueNum))
            {
                Console.WriteLine("[Warning] Received 'issues' event but 'issue.number' is missing.");
                return Results.Ok(new { status = "Ignored: Missing issue number" });
            }
            
            if (action == "opened")
            {
                var title = root.GetNestedStringSafe("issue", "title") ?? string.Empty;
                var body = root.GetNestedStringSafe("issue", "body") ?? string.Empty;
                
                string prompt = $"Analyze Issue #{issueNum}: {title}. {body}. Do NOT write code or create/modify any files/artifacts yet (do NOT write implementation_plan.md or any other markdown files). Formulate an implementation plan in your standard text response only. End by asking for a 👍 reaction to execute.";
                
                // Run headless agent (new session)
                string agentOutput = ExecuteAgyHeadless(localRepoPath, prompt);
                
                // Extract the newly generated DB ID and append it to the GitHub comment
                string newSessionId = ExtractConversationId(agentOutput);
                string cleanResponse = GetFinalResponseFromTranscript(newSessionId);
                if (string.IsNullOrEmpty(cleanResponse))
                {
                    cleanResponse = agentOutput;
                }
                PostGitHubComment(localRepoPath, issueNum, cleanResponse, newSessionId);
            }
            else if (action == "edited" || action == "labeled") 
            {
                // Triggered by a 👍 reaction
                string activeSessionId = GetSessionIdFromIssue(localRepoPath, issueNum);
                
                if (!string.IsNullOrEmpty(activeSessionId))
                {
                    string prompt = $"The plan for Issue #{issueNum} is approved. Create branch 'fix/issue-{issueNum}', implement the code, run local tests, and raise a PR via `gh pr create`.";
                    ExecuteAgyHeadless(localRepoPath, prompt, activeSessionId);
                    // Note: The PR creation is handled autonomously by agy via the prompt instructions
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
                    Console.WriteLine("[Warning] Received 'issue_comment' event but 'issue.number' or 'comment.body' is missing.");
                    return Results.Ok(new { status = "Ignored: Missing issue or comment data" });
                }
                
                string activeSessionId = GetSessionIdFromIssue(localRepoPath, issueNum);
                
                if (!string.IsNullOrEmpty(activeSessionId))
                {
                    bool isApproval = commentBody.Trim() == "👍" || 
                                       commentBody.Trim().Equals("lgtm", StringComparison.OrdinalIgnoreCase) || 
                                       commentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);
                    
                    string prompt;
                    if (isApproval)
                    {
                        prompt = $"The plan for Issue #{issueNum} is approved. Create branch 'fix/issue-{issueNum}', implement the code, run local tests, and raise a PR via `gh pr create`.";
                    }
                    else
                    {
                        prompt = $"Feedback received: '{commentBody}'. Please update the plan or code accordingly.";
                    }
                    
                    string agentOutput = ExecuteAgyHeadless(localRepoPath, prompt, activeSessionId);
                    string cleanResponse = GetFinalResponseFromTranscript(activeSessionId);
                    if (string.IsNullOrEmpty(cleanResponse))
                    {
                        cleanResponse = agentOutput;
                    }
                    
                    // Reply with the same active session ID embedded
                    PostGitHubComment(localRepoPath, issueNum, cleanResponse, activeSessionId);
                }
            }
        }

        return Results.Ok(new { status = "Success" });
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"[Error] Invalid JSON payload: {ex.Message}");
        return Results.BadRequest(new { error = "Invalid JSON payload" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Unexpected error in webhook: {ex.Message}\n{ex.StackTrace}");
        return Results.Ok(new { status = "Error", message = "An unexpected error occurred" });
    }
});

app.Run("http://0.0.0.0:8080");


// ─── NATIVE PROCESS EXECUTION WRAPPERS ──────────────────────────────────────

string ExecuteAgyHeadless(string repoPath, string prompt, string? conversationId = null)
{
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

    process.Start();
    
    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        Console.WriteLine($"[AGY ERROR] {error}");
        return $"Error executing agent: {error}";
    }

    return output;
}

string ExecuteProcess(string fileName, string workingDirectory, params string[] args)
{
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
    {
        process.StartInfo.ArgumentList.Add(arg);
    }

    process.Start();
    string output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    
    return output;
}

// ─── GITHUB & STATE MANAGEMENT HELPERS ──────────────────────────────────────

void PostGitHubComment(string repoPath, string issueNum, string body, string sessionId)
{
    // Append the hidden HTML tracking tag to the bottom of the Markdown body
    string payload = string.IsNullOrEmpty(sessionId) 
        ? body 
        : $"{body}\n\n<!-- agy-session-id: {sessionId} -->";

    ExecuteProcess("gh", repoPath, "issue", "comment", issueNum, "--body", payload);
}

string GetSessionIdFromIssue(string repoPath, string issueNum)
{
    // Use GitHub CLI to fetch the last few comments on the issue to find the tracking tag
    string commentsJson = ExecuteProcess("gh", repoPath, "issue", "view", issueNum, "--json", "comments");
    
    var match = Regex.Match(commentsJson, @"<!-- agy-session-id: ([a-zA-Z0-9\-]+) -->", RegexOptions.RightToLeft);
    return match.Success ? match.Groups[1].Value : string.Empty;
}

string ExtractConversationId(string agyOutput)
{
    // Regex to match a standard UUID format in the agy console output
    var match = Regex.Match(agyOutput, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
    return match.Success ? match.Value : string.Empty;
}

string GetFinalResponseFromTranscript(string sessionId)
{
    if (string.IsNullOrEmpty(sessionId)) return string.Empty;
    
    var transcriptPath = $"/root/.gemini/antigravity-cli/brain/{sessionId}/.system_generated/logs/transcript.jsonl";
    if (!File.Exists(transcriptPath))
    {
        return string.Empty;
    }
    
    string finalContent = string.Empty;
    try
    {
        foreach (var line in File.ReadLines(transcriptPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
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
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed lines in transcript.jsonl
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Reading transcript: {ex.Message}");
    }
    
    return finalContent;
}

public static class JsonElementExtensions
{
    public static string? GetStringSafe(this JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        }
        return null;
    }

    public static string? GetNestedStringSafe(this JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}