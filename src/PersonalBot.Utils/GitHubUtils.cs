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
            _logger.LogInformation("Pulling latest in '{localRepoPath}'", localRepoPath);
            await _processUtils.RunProcessAsync("git", ["pull"], localRepoPath, cancellationToken);
        }
    }

    public async Task PostGitHubCommentAsync(string repoPath, string issueNum, string body, string sessionId, CancellationToken cancellationToken = default)
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

    public async Task<string> GetSessionIdFromIssueAsync(string repoPath, string issueNum, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[GitHub] Fetching session ID from issue #{issueNum}", issueNum);
        string commentsJson = await _processUtils.RunProcessAsync("gh", ["issue", "view", issueNum, "--json", "comments"], repoPath, cancellationToken);
        var match = SessionIdRegex.Match(commentsJson);
        string sessionId = match.Success ? match.Groups[1].Value : string.Empty;
        _logger.LogInformation("[GitHub] Session ID for issue #{issueNum}: '{sessionId}'", issueNum, string.IsNullOrEmpty(sessionId) ? "none" : sessionId);
        return sessionId;
    }

    public static string GetIssueRepoPath(string repoName, string issueNum) => $"{WorkspaceBase}/{repoName}-issue-{issueNum}";

    public static bool IsOwnComment(string commentBody) => commentBody.Contains(BotWatermark);

    [GeneratedRegex(@"<!-- agy-session-id: ([a-zA-Z0-9\-]+) -->", RegexOptions.RightToLeft)]
    private static partial Regex SessionIdRegex { get; }
}
