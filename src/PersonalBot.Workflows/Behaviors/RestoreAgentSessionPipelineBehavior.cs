using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalBot.Data;
using PersonalBot.Data.Interfaces;

namespace PersonalBot.Workflows.Behaviors;

public class RestoreAgentSessionPipelineBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IHasIssueSession, IMessage
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RestoreAgentSessionPipelineBehavior<TMessage, TResponse>> _logger;

    public RestoreAgentSessionPipelineBehavior(IServiceScopeFactory scopeFactory, ILogger<RestoreAgentSessionPipelineBehavior<TMessage, TResponse>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        await SetMessageSessionAsync(message, cancellationToken);

        return await next(message, cancellationToken);
    }

    private async Task SetMessageSessionAsync(TMessage message, CancellationToken cancellationToken)
    {
        if (message.Session is null)
        {
            _logger.LogInformation("Trying to restore session for issue {IssueNumber}", message.IssueNumber);
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            message.Session = await dbContext.GetSessionFromIssueAsync(message.IssueNumber, message.Id, cancellationToken);
        }
    }
}
