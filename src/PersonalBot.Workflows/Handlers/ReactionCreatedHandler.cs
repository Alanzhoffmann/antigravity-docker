using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class ReactionCreatedHandler : INotificationHandler<ReactionCreated>
{
    private readonly GitHubUtils _gitHubUtils;
    private readonly ILogger<ReactionCreatedHandler> _logger;

    public ReactionCreatedHandler(GitHubUtils gitHubUtils, ILogger<ReactionCreatedHandler> logger)
    {
        _gitHubUtils = gitHubUtils;
        _logger = logger;
    }

    public async ValueTask Handle(ReactionCreated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reaction event: content='{ReactionContent}'", notification.ReactionContent);
        if (notification.ReactionContent != "+1" && notification.ReactionContent != "👍")
        {
            return;
        }

        if (string.IsNullOrEmpty(notification.IssueNumber))
        {
            _logger.LogWarning($"reaction event missing issue.number");
            return;
        }

        string localRepoPath = GitHubUtils.GetIssueRepoPath(notification.RepoName, notification.IssueNumber);
        await _gitHubUtils.EnsureRepoAsync(localRepoPath, notification.CloneUrl, notification.RepoName, notification.IssueNumber, cancellationToken);

        notification.ChildWorkflows.Add(
            new TaskApproved
            {
                RepoPath = localRepoPath,
                IssueNumber = notification.IssueNumber,
                RepoName = notification.RepoName,
            }
        );
    }
}
