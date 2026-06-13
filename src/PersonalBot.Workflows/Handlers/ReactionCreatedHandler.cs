using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class ReactionCreatedHandler : INotificationHandler<ReactionCreated>
{
    private readonly IPublisher _publisher;
    private readonly GitHubUtils _gitHubUtils;
    private readonly ILogger<ReactionCreatedHandler> _logger;

    public ReactionCreatedHandler(IPublisher publisher, GitHubUtils gitHubUtils, ILogger<ReactionCreatedHandler> logger)
    {
        _publisher = publisher;
        _gitHubUtils = gitHubUtils;
        _logger = logger;
    }

    public async ValueTask Handle(ReactionCreated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reaction event: content='{ReactionContent}'", notification.ReactionContent);
        if (notification.ReactionContent == "+1" || notification.ReactionContent == "👍")
        {
            if (string.IsNullOrEmpty(notification.IssueNumber))
            {
                _logger.LogWarning($"reaction event missing issue.number");
                return;
            }
            string localRepoPath = GitHubUtils.GetIssueRepoPath(notification.RepoName, notification.IssueNumber);
            await _gitHubUtils.EnsureRepoAsync(localRepoPath, notification.CloneUrl, notification.RepoName, notification.IssueNumber, cancellationToken);
            var sid = await _gitHubUtils.GetSessionIdFromIssueAsync(localRepoPath, notification.IssueNumber, cancellationToken);

            await _publisher.Publish(
                new TaskApproved
                {
                    RepoPath = localRepoPath,
                    IssueNumber = notification.IssueNumber,
                    SessionId = sid,
                },
                cancellationToken
            );
        }
    }
}
