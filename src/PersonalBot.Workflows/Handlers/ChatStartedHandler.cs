using Mediator;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class ChatStartedHandler : INotificationHandler<ChatStarted>
{
    private readonly IChatResolver _chatResolver;

    public ChatStartedHandler(IChatResolver chatResolver)
    {
        _chatResolver = chatResolver;
    }

    public async ValueTask Handle(ChatStarted notification, CancellationToken cancellationToken)
    {
        var agentChat = _chatResolver.ResolveCurrent();
        var response = await agentChat.GetResponseAsync(
            new()
            {
                RepoPath = notification.RepoPath,
                IssueNum = notification.IssueNumber,
                Prompt = notification.Prompt,
                Phase = notification.AgentPhase,
            },
            cancellationToken
        );

        notification.ChatOutput = response.Output;
        notification.ArtifactOutput = response.ArtifactOutput;
        notification.Session = response.Session;
        notification.AgentName = agentChat.AgentName;
    }
}
