using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class IssueCommentCreatedHandler : INotificationHandler<IssueCommentCreated>
{
    private readonly GitHubUtils _gitHubUtils;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IssueCommentCreatedHandler> _logger;

    public IssueCommentCreatedHandler(GitHubUtils gitHubUtils, IServiceScopeFactory scopeFactory, ILogger<IssueCommentCreatedHandler> logger)
    {
        _gitHubUtils = gitHubUtils;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ValueTask Handle(IssueCommentCreated notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(notification.IssueNumber) || string.IsNullOrEmpty(notification.CommentBody))
        {
            _logger.LogWarning($"issue_comment missing issue.number or comment.body");
            return ValueTask.CompletedTask;
        }

        if (GitHubUtils.IsOwnComment(notification.CommentBody))
        {
            _logger.LogInformation($"Skipping Bot comment to prevent loop");
            return ValueTask.CompletedTask;
        }

        bool isApproval =
            notification.CommentBody.Trim() == "👍"
            || notification.CommentBody.Trim().Equals("lgtm", StringComparison.OrdinalIgnoreCase)
            || notification.CommentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);

        notification.ChildWorkflows.Add(
            isApproval
                ? new TaskApproved { IssueNumber = notification.IssueNumber, RepoName = notification.RepoName }
                : new FeedbackReceived
                {
                    IssueNumber = notification.IssueNumber,
                    RepoName = notification.RepoName,
                    CommentBody = notification.CommentBody,
                }
        );

        return ValueTask.CompletedTask;
    }
}
