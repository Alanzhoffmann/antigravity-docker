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
    var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
    using var document = await JsonDocument.ParseAsync(context.Request.Body);
    var root = document.RootElement;
    
    // Graceful parsing using TryGetProperty to prevent crashes on malformed payloads
    if (!root.TryGetProperty("repository", out var repoElement) || 
        !repoElement.TryGetProperty("name", out var nameElement))
    {
        return Results.Ok(new { status = "Ignored: Missing repository data" });
    }

    var repoName = nameElement.GetString();
    if (string.IsNullOrEmpty(repoName) || !supportedRepos.Contains(repoName))
    {
        return Results.Ok(new { status = "Ignored: Unsupported repository" });
    }

    var localRepoPath = $"{workspaceBase}/{repoName}";
    var action = root.GetProperty("action").GetString();

    // Ensure repo exists locally
    if (!Directory.Exists(localRepoPath))
    {
        var cloneUrl = repoElement.GetProperty("clone_url").GetString();
        ExecuteProcess("git", workspaceBase, "clone", cloneUrl!, localRepoPath);
    }
    else
    {
        ExecuteProcess("git", localRepoPath, "pull");
    }

    // ─── ISSUE EVENTS (PLANNING & EXECUTION) ─────────────────────────────────
    if (eventType == "issues")
    {
        var issueNum = root.GetProperty("issue").GetProperty("number").ToString()!;
        
        if (action == "opened")
        {
            var title = root.GetProperty("issue").GetProperty("title").GetString();
            var body = root.GetProperty("issue").GetProperty("body").GetString();
            
            string prompt = $"Analyze Issue #{issueNum}: {title}. {body}. Do NOT write code yet. Formulate an implementation plan. End by asking for a 👍 reaction to execute.";
            
            // Run headless agent (new session)
            string agentOutput = ExecuteAgyHeadless(localRepoPath, prompt);
            
            // Extract the newly generated DB ID and append it to the GitHub comment
            string newSessionId = ExtractConversationId(agentOutput);
            PostGitHubComment(localRepoPath, issueNum, agentOutput, newSessionId);
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
        var userType = root.GetProperty("comment").GetProperty("user").GetProperty("type").GetString();
        
        if (userType != "Bot") // Prevent infinite bot-looping
        {
            var issueNum = root.GetProperty("issue").GetProperty("number").ToString()!;
            var commentBody = root.GetProperty("comment").GetProperty("body").GetString()!;
            
            string activeSessionId = GetSessionIdFromIssue(localRepoPath, issueNum);
            
            if (!string.IsNullOrEmpty(activeSessionId))
            {
                string prompt = $"Feedback received: '{commentBody}'. Please update the plan or code accordingly.";
                string agentOutput = ExecuteAgyHeadless(localRepoPath, prompt, activeSessionId);
                
                // Reply with the same active session ID embedded
                PostGitHubComment(localRepoPath, issueNum, agentOutput, activeSessionId);
            }
        }
    }

    return Results.Ok();
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
        : $"{body}\n\n";

    ExecuteProcess("gh", repoPath, "issue", "comment", issueNum, "--body", payload);
}

string GetSessionIdFromIssue(string repoPath, string issueNum)
{
    // Use GitHub CLI to fetch the last few comments on the issue to find the tracking tag
    string commentsJson = ExecuteProcess("gh", repoPath, "issue", "view", issueNum, "--json", "comments");
    
    var match = Regex.Match(commentsJson, @"", RegexOptions.RightToLeft);
    return match.Success ? match.Groups[1].Value : string.Empty;
}

string ExtractConversationId(string agyOutput)
{
    // Regex to match a standard UUID format in the agy console output
    var match = Regex.Match(agyOutput, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
    return match.Success ? match.Value : string.Empty;
}