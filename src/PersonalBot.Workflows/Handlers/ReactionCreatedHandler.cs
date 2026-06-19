using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class ReactionCreatedHandler : INotificationHandler<ReactionCreated>
{
    private readonly ILogger<ReactionCreatedHandler> _logger;

    public ReactionCreatedHandler(ILogger<ReactionCreatedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(ReactionCreated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reaction event: content='{ReactionContent}'", notification.ReactionContent);
        if (notification.ReactionContent != "+1" && notification.ReactionContent != "👍")
        {
            return ValueTask.CompletedTask;
        }

        if (string.IsNullOrEmpty(notification.IssueNumber))
        {
            _logger.LogWarning($"reaction event missing issue.number");
            return ValueTask.CompletedTask;
        }

        notification.ChildWorkflows.Add(new TaskApproved { IssueNumber = notification.IssueNumber, RepoName = notification.RepoName });

        return ValueTask.CompletedTask;
    }
}
