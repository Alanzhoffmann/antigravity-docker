using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class WebhookReceivedHandler : INotificationHandler<WebhookReceived>
{
    private readonly ILogger<WebhookReceivedHandler> _logger;

    public WebhookReceivedHandler(ILogger<WebhookReceivedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(WebhookReceived notification, CancellationToken cancellationToken)
    {
        switch (notification)
        {
            case { EventType: "issues", Action: "opened", IssueNumber: not null and { Length: > 0 } }:
                notification.ChildWorkflows.Add(
                    new IssueOpened
                    {
                        IssueNumber = notification.IssueNumber,
                        IssueTitle = notification.IssueTitle,
                        IssueBody = notification.IssueBody,
                        RepoName = notification.RepoName,
                        CloneUrl = notification.CloneUrl,
                    }
                );
                break;
            case { EventType: "reaction", Action: "created", IssueNumber: not null and { Length: > 0 } }:
                notification.ChildWorkflows.Add(
                    new ReactionCreated
                    {
                        ReactionContent = notification.ReactionContent,
                        IssueNumber = notification.IssueNumber,
                        RepoName = notification.RepoName,
                        CloneUrl = notification.CloneUrl,
                    }
                );
                break;
            case { EventType: "issue_comment", Action: "created", IssueNumber: not null and { Length: > 0 } }:
                notification.ChildWorkflows.Add(
                    new IssueCommentCreated
                    {
                        IssueNumber = notification.IssueNumber,
                        CommentBody = notification.CommentBody,
                        RepoName = notification.RepoName,
                        CloneUrl = notification.CloneUrl,
                    }
                );
                break;
            case { EventType: "pull_request", Action: "closed" }:
                notification.ChildWorkflows.Add(
                    new PullRequestClosed
                    {
                        PrMerged = notification.PrMerged,
                        HeadRef = notification.HeadRef,
                        RepoName = notification.RepoName,
                    }
                );
                break;
            default:
                _logger.LogInformation("Unhandled webhook {Webhook} — no-op", notification);
                break;
        }

        return ValueTask.CompletedTask;
    }
}
