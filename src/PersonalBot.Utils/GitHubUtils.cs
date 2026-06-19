using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PersonalBot.Utils;

public partial class GitHubUtils
{
    private const string BotWatermark = "<!-- from-bot: true -->";
    const string WorkspaceBase = "/app/workspaces";

    private readonly ProcessUtils _processUtils;
    private readonly ILogger<GitHubUtils> _logger;

    public GitHubUtils(ProcessUtils processUtils, ILogger<GitHubUtils> logger)
    {
        _processUtils = processUtils;
        _logger = logger;
    }

    public async Task EnsureRepoAsync(string localRepoPath, string? cloneUrl, string repoName, string issueNum, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(localRepoPath))
        {
            if (string.IsNullOrEmpty(cloneUrl))
            {
                _logger.LogWarning("No clone_url for {repoName}#{issueNum}", repoName, issueNum);
                return;
            }

            _logger.LogInformation("Cloning '{cloneUrl}' -> '{localRepoPath}'", cloneUrl, localRepoPath);
            await _processUtils.RunProcessAsync("git", ["clone", cloneUrl, localRepoPath], WorkspaceBase, cancellationToken);
        }
        else
        {
            await RebaseOntoMainAsync(localRepoPath, cancellationToken: cancellationToken);
        }
    }

    public async Task PostGitHubCommentAsync(string repoPath, string issueNum, string body, string? sessionId, CancellationToken cancellationToken = default)
    {
        string payload = string.IsNullOrEmpty(sessionId) ? body : $"{body}\n\n<!-- agy-session-id: {sessionId} -->";

        payload += $"\n\n{BotWatermark}";

        _logger.LogInformation("Posting comment on issue #{issueNum} (session='{sessionId}', length={PayloadLength})", issueNum, sessionId, payload.Length);

        await _processUtils.RunProcessAsync("gh", ["issue", "comment", issueNum, "--body", payload], repoPath, cancellationToken);
    }

    public async Task<string?> GetSessionIdFromIssueAsync(string repoPath, string issueNum, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching session ID from issue #{issueNum}", issueNum);
        string commentsJson = await _processUtils.RunProcessAsync("gh", ["issue", "view", issueNum, "--json", "comments"], repoPath, cancellationToken);
        var match = SessionIdRegex.Match(commentsJson);
        var sessionId = match.Success ? match.Groups[1].Value : null;
        _logger.LogInformation("Session ID for issue #{issueNum}: '{sessionId}'", issueNum, string.IsNullOrEmpty(sessionId) ? "none" : sessionId);
        return sessionId;
    }

    public async Task<int> GetCommitsAheadOfMainAsync(string repoPath, string baseBranch = "main", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking commits ahead of '{BaseBranch}' in '{RepoPath}'", baseBranch, repoPath);

        try
        {
            // 'git rev-list --count main..HEAD' returns exactly how many commits HEAD has that main doesn't.
            string output = await _processUtils.RunProcessAsync("git", ["rev-list", "--count", $"{baseBranch}..HEAD"], repoPath, cancellationToken);

            if (int.TryParse(output.Trim(), out int count))
            {
                _logger.LogInformation("Branch is {Count} commit(s) ahead of {BaseBranch}.", count, baseBranch);
                return count;
            }

            _logger.LogWarning("Could not parse commit count from output: '{Output}'", output);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check commits ahead of {BaseBranch}. Are you on a valid branch?", baseBranch);
            return 0;
        }
    }

    public async Task RebaseOntoMainAsync(string repoPath, string baseBranch = "main", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching latest '{BaseBranch}' and rebasing current branch in '{RepoPath}'", baseBranch, repoPath);

        try
        {
            // 1. We MUST fetch from origin first since you mentioned 'main' was updated separately (remotely)
            await _processUtils.RunProcessAsync("git", ["fetch", "origin", baseBranch], repoPath, cancellationToken);

            // 2. Rebase onto the newly fetched remote branch
            await _processUtils.RunProcessAsync("git", ["rebase", $"origin/{baseBranch}"], repoPath, cancellationToken);

            _logger.LogInformation("Successfully rebased onto origin/{BaseBranch}.", baseBranch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rebase onto {BaseBranch} failed (likely due to a merge conflict). Aborting rebase to protect workspace state.", baseBranch);

            // --- CRITICAL SAFETY NET ---
            // If rebase fails, Git freezes the repo in a "rebasing" state. We must abort so the agent doesn't get permanently stuck.
            try
            {
                await _processUtils.RunProcessAsync("git", ["rebase", "--abort"], repoPath, cancellationToken);
                _logger.LogInformation("Rebase aborted successfully. Workspace restored to previous state.");
            }
            catch (Exception abortEx)
            {
                _logger.LogCritical(abortEx, "Failed to abort rebase! The repository at {RepoPath} requires manual intervention.", repoPath);
            }

            // Throw so the calling execution workflow knows the rebase failed and doesn't proceed blindly
            throw new InvalidOperationException($"Failed to rebase onto {baseBranch}. See logs for details.", ex);
        }
    }

    public static string GetIssueRepoPath(string repoName, string issueNum) => $"{WorkspaceBase}/{repoName}-issue-{issueNum}";

    public static bool IsOwnComment(string commentBody) => commentBody.Contains(BotWatermark);

    [GeneratedRegex(@"<!-- agy-session-id: ([a-zA-Z0-9\-]+) -->", RegexOptions.RightToLeft)]
    private static partial Regex SessionIdRegex { get; }
}
