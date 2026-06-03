using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var supportedRepos = new HashSet<string> { "antigravity-docker" };
string workspaceBase = "/app/workspaces";

app.MapGet("/", () => "Antigravity Bot API is running. Awaiting GitHub webhooks...");

app.MapPost("/github-webhook", async (HttpContext context) =>
{

    var eventType = context.Request.Headers["X-Github-Event"].ToString();
    using var document = await JsonDocument.ParseAsync(context.Request.Body);
    var root = document.RootElement;
    
    var repoName = root.GetProperty("repository").GetProperty("name").GetString();
    if (string.IsNullOrEmpty(repoName) || !supportedRepos.Contains(repoName))
    {
        return Results.Ok(new { status = "Ignored unsupported repository" });
    }

    var localRepoPath = $"{workspaceBase}/{repoName}";
    var action = root.GetProperty("action").GetString();

    // Ensure the repository exists locally before the agent tries to enter it
    if (!Directory.Exists(localRepoPath))
    {
        var cloneUrl = root.GetProperty("repository").GetProperty("clone_url").GetString();
        ExecuteShellCommand($"git clone {cloneUrl} {localRepoPath}");
    }

    // ─── ISSUE EVENTS (PLANNING & EXECUTION) ─────────────────────────────────
    if (eventType == "issues")
    {
        var issueNum = root.GetProperty("issue").GetProperty("number").ToString();
        var sessionName = $"agy_{repoName}_issue_{issueNum}";

        if (action == "opened")
        {
            var title = root.GetProperty("issue").GetProperty("title").GetString();
            var body = root.GetProperty("issue").GetProperty("body").GetString();
            
            string prompt = $"/goal \"Analyze Issue #{issueNum}: {title}. {body}. Do NOT write code yet. Formulate an implementation plan and use the GitHub CLI (`gh issue comment {issueNum} --body '...'`) to post it. End by asking for a 👍 reaction to execute.\"";
            
            StartAgySession(sessionName, localRepoPath, prompt);
        }
        else if (action == "edited" || action == "labeled") 
        {
            // Triggers when the issue is updated (e.g., a thumbs-up reaction triggers a payload edit)
            string prompt = $"/goal \"The plan for Issue #{issueNum} is approved. Create branch 'fix/issue-{issueNum}', implement the code, run local verification tests, and raise a PR via `gh pr create`.\"";
            StartAgySession(sessionName, localRepoPath, prompt);
        }
        else if (action == "closed")
        {
            // Garbage Collection: The issue is done, tear down the terminal runtime
            ExecuteShellCommand($"tmux kill-session -t {sessionName}");
        }
    }
    // ─── ISSUE COMMENTS (FEEDBACK LOOP) ──────────────────────────────────────
    else if (eventType == "issue_comment" && action == "created")
    {
        var userType = root.GetProperty("comment").GetProperty("user").GetProperty("type").GetString();
        if (userType != "Bot")
        {
            var issueNum = root.GetProperty("issue").GetProperty("number").ToString();
            var sessionName = $"agy_{repoName}_issue_{issueNum}";
            var commentBody = root.GetProperty("comment").GetProperty("body").GetString();
            
            string prompt = $"/goal \"Feedback on Issue #{issueNum}: '{commentBody}'. Update the plan if needed or defend your architectural choices. Reply using `gh issue comment {issueNum}`.\"";
            ExecuteShellCommand($"tmux send-keys -t {sessionName} \"{prompt}\" Enter");
        }
    }
    // ─── PULL REQUEST EVENTS (CODE REVIEW) ───────────────────────────────────
    else if (eventType == "pull_request")
    {
        var prNum = root.GetProperty("pull_request").GetProperty("number").ToString();
        var sessionName = $"agy_{repoName}_pr_{prNum}";

        if (action == "opened")
        {
            string prompt = $"/goal \"PR #{prNum} opened. Checkout with `gh pr checkout {prNum}`, review the diff thoroughly for bugs or missing tests, and post review comments using `gh pr review {prNum}`.\"";
            StartAgySession(sessionName, localRepoPath, prompt);
        }
        else if (action == "closed")
        {
            ExecuteShellCommand($"tmux kill-session -t {sessionName}");
        }
    }
    // ─── PULL REQUEST COMMENTS (INLINE FIXES) ────────────────────────────────
    else if (eventType == "pull_request_review_comment" && action == "created")
    {
         var userType = root.GetProperty("comment").GetProperty("user").GetProperty("type").GetString();
         if (userType != "Bot")
         {
             var prNum = root.GetProperty("pull_request").GetProperty("number").ToString();
             
             // Route PR fixes back to the dev session that created the PR
             var sessionName = $"agy_{repoName}_issue_{prNum}"; // Assuming PR number aligns with the issue branch context
             
             var commentBody = root.GetProperty("comment").GetProperty("body").GetString();
             var filePath = root.GetProperty("comment").GetProperty("path").GetString();
             
             string prompt = $"/goal \"Fix the codebase based on the PR comment: '{commentBody}' in file `{filePath}`. Verify tests pass and push the changes back to the active branch.\"";
             ExecuteShellCommand($"tmux send-keys -t {sessionName} \"{prompt}\" Enter");
         }
    }

    return Results.Ok();
});

app.Run("http://0.0.0.0:8080");

// ─── HELPER FUNCTIONS ────────────────────────────────────────────────────────

void StartAgySession(string sessionName, string path, string initialCommand)
{
    var check = Process.Start(new ProcessStartInfo { 
        FileName = "tmux", 
        Arguments = $"has-session -t {sessionName}", 
        RedirectStandardOutput = true 
    });
    check!.WaitForExit();
    
    if (check.ExitCode != 0)
    {
        ExecuteShellCommand($"tmux new-session -d -s {sessionName} bash");
        Thread.Sleep(500); 
        
        ExecuteShellCommand($"tmux send-keys -t {sessionName} \"cd {path}\" Enter");
        Thread.Sleep(500);
        
        ExecuteShellCommand($"tmux send-keys -t {sessionName} \"agy\" Enter");
        Thread.Sleep(1500); 
    }
    
    // Use the new buffer method to send the complex prompt safely
    SendPromptViaBuffer(sessionName, initialCommand);
}

void SendPromptViaBuffer(string sessionName, string promptText)
{
    // 1. Write the raw, unescaped text to a temporary file
    string tempFilePath = Path.GetTempFileName();
    File.WriteAllText(tempFilePath, promptText);

    // 2. Load the file directly into the tmux clipboard buffer
    ExecuteShellCommand($"tmux load-buffer {tempFilePath}");

    // 3. Type the /goal command initiator
    ExecuteShellCommand($"tmux send-keys -t {sessionName} \"/goal \"");

    // 4. Paste the buffer. This bypasses Bash completely and acts as if 
    // a human physically typed the exact characters into the TUI.
    ExecuteShellCommand($"tmux paste-buffer -t {sessionName}");

    // 5. Hit Enter to execute, then clean up the temp file
    ExecuteShellCommand($"tmux send-keys -t {sessionName} Enter");
    File.Delete(tempFilePath);
}

void ExecuteShellCommand(string command)
{
    var escapedArgs = command.Replace("\"", "\\\"");
    
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{escapedArgs}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true, // Capture errors to prevent silent failures
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };

    process.Start();
    
    // Read the output for logging (optional, but great for debugging)
    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();
    
    process.WaitForExit(); // THIS IS THE CRITICAL FIX

    if (process.ExitCode != 0)
    {
        Console.WriteLine($"[BASH ERROR] {error}");
    }
}