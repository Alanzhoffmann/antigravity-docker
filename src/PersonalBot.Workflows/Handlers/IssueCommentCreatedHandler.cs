using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class IssueCommentCreatedHandler : IRequestHandler<IssueCommentCreated>
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

    public ValueTask<Unit> Handle(IssueCommentCreated request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.IssueNumber) || string.IsNullOrEmpty(request.CommentBody))
        {
            _logger.LogWarning($"issue_comment missing issue.number or comment.body");
            return Unit.ValueTask;
        }

        if (GitHubUtils.IsOwnComment(request.CommentBody))
        {
            _logger.LogInformation($"Skipping Bot comment to prevent loop");
            return Unit.ValueTask;
        }

        bool isApproval =
            request.CommentBody.Trim() == "👍"
            || request.CommentBody.Trim().Equals("lgtm", StringComparison.OrdinalIgnoreCase)
            || request.CommentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);

        request.ChildWorkflows.Add(
            isApproval
                ? new TaskApproved { IssueNumber = request.IssueNumber, RepoName = request.RepoName }
                : new FeedbackReceived
                {
                    IssueNumber = request.IssueNumber,
                    RepoName = request.RepoName,
                    CommentBody = request.CommentBody,
                }
        );

        return Unit.ValueTask;
    }
}
