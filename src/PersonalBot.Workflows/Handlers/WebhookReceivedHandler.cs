using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class WebhookReceivedHandler : INotificationHandler<WebhookReceived>
{
    private readonly BotDbContext _context;
    private readonly ILogger<WebhookReceivedHandler> _logger;

    public WebhookReceivedHandler(BotDbContext context, ILogger<WebhookReceivedHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async ValueTask Handle(WebhookReceived notification, CancellationToken cancellationToken)
    {
        switch (notification)
        {
            case { EventType: "issues", Action: "opened" }:
                _context.Add(
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
                _context.Add(
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
                _context.Add(
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
                _context.Add(
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

        await _context.SaveChangesAsync();
    }
}
