using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class ChatIssueReplyHandler : IRequestHandler<ChatIssueReply>
{
    private readonly GitHubUtils _gitHubUtils;
    private readonly ILogger<ChatIssueReplyHandler> _logger;

    public ChatIssueReplyHandler(GitHubUtils gitHubUtils, ILogger<ChatIssueReplyHandler> logger)
    {
        _gitHubUtils = gitHubUtils;
        _logger = logger;
    }

    public async ValueTask<Unit> Handle(ChatIssueReply request, CancellationToken cancellationToken)
    {
        var parentNotification = request.ParentWorkflow;
        switch (parentNotification)
        {
            case ChatStarted chatStarted:
                var comment = !string.IsNullOrEmpty(chatStarted.ArtifactOutput)
                    ? chatStarted.ArtifactOutput
                    : chatStarted.ChatOutput ?? $"empty output for {chatStarted.AgentName}";

                var issueWebhook = (IIsIssueWebhook)chatStarted;

                await _gitHubUtils.PostGitHubCommentAsync(issueWebhook.RepoPath, issueWebhook.IssueNumber, comment, chatStarted.Session, cancellationToken);
                break;
            default:
                _logger.LogWarning("ChatIssueReply with no parent task, this should not happen");
                break;
        }

        return Unit.Value;
    }
}
