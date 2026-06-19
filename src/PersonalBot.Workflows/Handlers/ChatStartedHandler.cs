using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class ChatStartedHandler : INotificationHandler<ChatStarted>
{
    private readonly IChatResolver _chatResolver;
    private readonly ILogger<ChatStartedHandler> _logger;

    public ChatStartedHandler(IChatResolver chatResolver, ILogger<ChatStartedHandler> logger)
    {
        _chatResolver = chatResolver;
        _logger = logger;
    }

    public async ValueTask Handle(ChatStarted notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting agent for issue #{issueNum}", notification.IssueNumber);

        var agentChat = _chatResolver.ResolveCurrent();
        var response = await agentChat.GetResponseAsync(
            notification.RepoPath,
            notification.Prompt,
            notification.AgentPhase,
            notification.Session,
            cancellationToken
        );

        notification.ChatOutput = response.Output;
        notification.ArtifactOutput = response.ArtifactOutput;
        notification.Session = response.Session;
        notification.AgentName = agentChat.AgentName;

        _logger.LogInformation("{issueKey} processing complete", $"{notification.RepoName}#{notification.IssueNumber}");
    }
}
