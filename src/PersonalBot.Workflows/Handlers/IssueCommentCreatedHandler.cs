using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class IssueCommentCreatedHandler : INotificationHandler<IssueCommentCreated>
{
    private readonly IPublisher _publisher;
    private readonly GitHubUtils _gitHubUtils;
    private readonly IChatResolver _chatResolver;
    private readonly ILogger<IssueCommentCreatedHandler> _logger;

    public IssueCommentCreatedHandler(IPublisher publisher, GitHubUtils gitHubUtils, IChatResolver chatResolver, ILogger<IssueCommentCreatedHandler> logger)
    {
        _publisher = publisher;
        _gitHubUtils = gitHubUtils;
        _chatResolver = chatResolver;
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
            await _publisher.Publish(
                new TaskApproved
                {
                    RepoPath = localRepoPath,
                    IssueNumber = notification.IssueNumber,
                    SessionId = activeSessionId,
                },
                cancellationToken
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
            var agentChat = _chatResolver.ResolveCurrent();
            var response = await agentChat.GetResponseAsync(
                new()
                {
                    RepoPath = localRepoPath,
                    IssueNum = notification.IssueNumber,
                    Prompt = execPrompt,
                    Phase = AgentPhase.Planning,
                },
                cancellationToken
            );
            var comment = !string.IsNullOrEmpty(response.ArtifactOutput) ? response.ArtifactOutput : response.Output;
            await _gitHubUtils.PostGitHubCommentAsync(localRepoPath, notification.IssueNumber, comment, activeSessionId, cancellationToken);
        }
    }
}
