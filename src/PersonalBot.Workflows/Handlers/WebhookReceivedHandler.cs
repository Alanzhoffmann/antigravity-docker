using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalBot.Data;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class WebhookReceivedHandler : INotificationHandler<WebhookReceived>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookReceivedHandler> _logger;

    public WebhookReceivedHandler(IServiceScopeFactory scopeFactory, ILogger<WebhookReceivedHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async ValueTask Handle(WebhookReceived notification, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        switch (notification)
        {
            case { EventType: "issues", Action: "opened" }:
                context.Add(
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
            case { EventType: "reaction", Action: "created" }:
                context.Add(
                    new ReactionCreated
                    {
                        ReactionContent = notification.ReactionContent,
                        IssueNumber = notification.IssueNumber,
                        RepoName = notification.RepoName,
                        CloneUrl = notification.CloneUrl,
                    }
                );
                break;
            case { EventType: "issue_comment", Action: "created" }:
                context.Add(
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
                context.Add(
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

        await context.SaveChangesAsync(cancellationToken);
    }
}
