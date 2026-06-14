using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalBot.Data;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class ReactionCreatedHandler : INotificationHandler<ReactionCreated>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GitHubUtils _gitHubUtils;
    private readonly ILogger<ReactionCreatedHandler> _logger;

    public ReactionCreatedHandler(IServiceScopeFactory scopeFactory, GitHubUtils gitHubUtils, ILogger<ReactionCreatedHandler> logger)
    {
        _scopeFactory = scopeFactory;
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

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            context.Add(
                new TaskApproved
                {
                    RepoPath = localRepoPath,
                    IssueNumber = notification.IssueNumber,
                    SessionId = sid,
                }
            );
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
