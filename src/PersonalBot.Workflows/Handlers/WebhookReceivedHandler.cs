using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class WebhookReceivedHandler : IRequestHandler<WebhookReceived>
{
    private readonly ILogger<WebhookReceivedHandler> _logger;

    public WebhookReceivedHandler(ILogger<WebhookReceivedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<Unit> Handle(WebhookReceived request, CancellationToken cancellationToken)
    {
        switch (request)
        {
            case { EventType: "issues", Action: "opened", IssueNumber: not null and { Length: > 0 } }:
                request.ChildWorkflows.Add(
                    new IssueOpened
                    {
                        IssueNumber = request.IssueNumber,
                        IssueTitle = request.IssueTitle,
                        IssueBody = request.IssueBody,
                        RepoName = request.RepoName,
                        CloneUrl = request.CloneUrl,
                    }
                );
                break;
            case { EventType: "reaction", Action: "created", IssueNumber: not null and { Length: > 0 } }:
                request.ChildWorkflows.Add(
                    new ReactionCreated
                    {
                        ReactionContent = request.ReactionContent,
                        IssueNumber = request.IssueNumber,
                        RepoName = request.RepoName,
                        CloneUrl = request.CloneUrl,
                    }
                );
                break;
            case { EventType: "issue_comment", Action: "created", IssueNumber: not null and { Length: > 0 } }:
                request.ChildWorkflows.Add(
                    new IssueCommentCreated
                    {
                        IssueNumber = request.IssueNumber,
                        CommentBody = request.CommentBody,
                        RepoName = request.RepoName,
                        CloneUrl = request.CloneUrl,
                    }
                );
                break;
            case { EventType: "pull_request", Action: "closed" }:
                request.ChildWorkflows.Add(
                    new PullRequestClosed
                    {
                        PrMerged = request.PrMerged,
                        HeadRef = request.HeadRef,
                        RepoName = request.RepoName,
                    }
                );
                break;
            default:
                _logger.LogInformation("Unhandled webhook {Webhook} — no-op", request);
                break;
        }

        return Unit.ValueTask;
    }
}
