using PersonalBot.Utils;

namespace PersonalBot.Data.Interfaces;

public interface IIsIssueWebhook
{
    string RepoName { get; }
    string IssueNumber { get; }
    string RepoPath => GitHubUtils.GetIssueRepoPath(RepoName, IssueNumber);
    string? CloneUrl => null;
}
