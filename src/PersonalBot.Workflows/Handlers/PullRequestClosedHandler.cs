using System.Text.RegularExpressions;
using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public partial class PullRequestClosedHandler : INotificationHandler<PullRequestClosed>
{
    private readonly ILogger<PullRequestClosedHandler> _logger;

    public PullRequestClosedHandler(ILogger<PullRequestClosedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(PullRequestClosed notification, CancellationToken cancellationToken)
    {
        if (notification.PrMerged)
        {
            _logger.LogInformation("PR merged head_ref='{headRef}'", notification.HeadRef);
            var issueMatch = IssueRegex.Match(notification.HeadRef ?? string.Empty);
            if (issueMatch.Success)
            {
                string issueNum = issueMatch.Groups[1].Value;
                string path = GitHubUtils.GetIssueRepoPath(notification.RepoName, issueNum);
                if (Directory.Exists(path))
                {
                    _logger.LogInformation("Deleting isolated clone for issue #{issueNum}: '{path}'", issueNum, path);
                    try
                    {
                        Directory.Delete(path, recursive: true);
                        _logger.LogInformation("Deleted '{path}'", path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete '{Path}': {ExceptionMessage}", path, ex.Message);
                    }
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    [GeneratedRegex(@"fix/issue-(\d+)")]
    private static partial Regex IssueRegex { get; }
}
