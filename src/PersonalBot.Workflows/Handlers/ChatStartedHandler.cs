using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class ChatStartedHandler : IRequestHandler<ChatStarted>
{
    private readonly IChatResolver _chatResolver;
    private readonly ILogger<ChatStartedHandler> _logger;

    public ChatStartedHandler(IChatResolver chatResolver, ILogger<ChatStartedHandler> logger)
    {
        _chatResolver = chatResolver;
        _logger = logger;
    }

    public async ValueTask<Unit> Handle(ChatStarted request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting agent for issue #{issueNum}", request.IssueNumber);

        var agentChat = _chatResolver.ResolveCurrent();
        var response = await agentChat.GetResponseAsync(
            ((IIsIssueWebhook)request).RepoPath,
            request.Prompt,
            request.AgentPhase,
            request.Session,
            cancellationToken
        );

        request.ChatOutput = response.Output;
        request.ArtifactOutput = response.ArtifactOutput;
        request.Session = response.Session;

        _logger.LogInformation("{issueKey} processing complete", $"{request.RepoName}#{request.IssueNumber}");

        return Unit.Value;
    }
}
