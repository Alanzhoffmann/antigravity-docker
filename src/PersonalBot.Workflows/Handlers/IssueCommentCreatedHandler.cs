using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class IssueCommentCreatedHandler : INotificationHandler<IssueCommentCreated>
{
    private readonly GitHubUtils _gitHubUtils;
    private readonly ILogger<IssueCommentCreatedHandler> _logger;

    public IssueCommentCreatedHandler(GitHubUtils gitHubUtils, ILogger<IssueCommentCreatedHandler> logger)
    {
        _gitHubUtils = gitHubUtils;
        _logger = logger;
    }

    public async ValueTask Handle(IssueCommentCreated notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(notification.IssueNumber) || string.IsNullOrEmpty(notification.CommentBody))
        {
            _logger.LogWarning($"issue_comment missing issue.number or comment.body");
            return;
        }

        if (GitHubUtils.IsOwnComment(notification.CommentBody))
        {
            _logger.LogInformation($"Skipping Bot comment to prevent loop");
            return;
        }

        string localRepoPath = GitHubUtils.GetIssueRepoPath(notification.RepoName, notification.IssueNumber);
        await _gitHubUtils.EnsureRepoAsync(localRepoPath, notification.CloneUrl, notification.RepoName, notification.IssueNumber, cancellationToken);

        var activeSessionId = await _gitHubUtils.GetSessionIdFromIssueAsync(localRepoPath, notification.IssueNumber, cancellationToken);
        _logger.LogInformation("issue_comment #{issueNum} activeSession='{activeSessionId}'", notification.IssueNumber, activeSessionId);
        if (string.IsNullOrEmpty(activeSessionId))
        {
            _logger.LogWarning("No active session for #{issueNum}", notification.IssueNumber);
            return;
        }

        bool isApproval =
            notification.CommentBody.Trim() == "👍"
            || notification.CommentBody.Trim().Equals("lgtm", StringComparison.OrdinalIgnoreCase)
            || notification.CommentBody.Trim().Equals("approved", StringComparison.OrdinalIgnoreCase);

        if (isApproval)
        {
            notification.ChildWorkflows.Add(
                new TaskApproved
                {
                    RepoPath = localRepoPath,
                    IssueNumber = notification.IssueNumber,
                    SessionId = activeSessionId,
                }
            );
        }
        else
        {
            string execPrompt =
                $"Feedback received on Issue #{notification.IssueNumber}: '{notification.CommentBody}'. Update the plan accordingly. Post an updated plan artifact and ask for another 👍 to proceed.";

            _logger.LogInformation(
                "Feedback on #{issueNum}: '{CommentBody}'",
                notification.IssueNumber,
                notification.CommentBody[..Math.Min(80, notification.CommentBody.Length)]
            );

            notification.ChildWorkflows.Add(
                new ChatStarted
                {
                    RepoPath = localRepoPath,
                    IssueNumber = notification.IssueNumber,
                    Prompt = execPrompt,
                    AgentPhase = AgentPhase.Planning,
                    ChildWorkflows = [new ChatIssueReply()],
                }
            );
        }
    }
}
