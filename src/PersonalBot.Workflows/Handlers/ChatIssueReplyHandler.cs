using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class ChatIssueReplyHandler : INotificationHandler<ChatIssueReply>
{
    private readonly GitHubUtils _gitHubUtils;
    private readonly ILogger<ChatIssueReplyHandler> _logger;

    public ChatIssueReplyHandler(GitHubUtils gitHubUtils, ILogger<ChatIssueReplyHandler> logger)
    {
        _gitHubUtils = gitHubUtils;
        _logger = logger;
    }

    public async ValueTask Handle(ChatIssueReply notification, CancellationToken cancellationToken)
    {
        var parentNotification = notification.ParentWorkflow;
        if (parentNotification is ChatStarted chatStarted)
        {
            var comment = !string.IsNullOrEmpty(chatStarted.ArtifactOutput)
                ? chatStarted.ArtifactOutput
                : chatStarted.ChatOutput ?? $"empty output for {chatStarted.AgentName}";
            await _gitHubUtils.PostGitHubCommentAsync(chatStarted.RepoPath, chatStarted.IssueNumber, comment, chatStarted.Session, cancellationToken);
            return;
        }

        _logger.LogWarning("ChatIssueReply with no parent task, this should not happen");
    }
}
