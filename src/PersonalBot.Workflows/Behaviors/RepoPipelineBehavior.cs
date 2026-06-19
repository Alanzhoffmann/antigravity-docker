using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Interfaces;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Behaviors;

public class RepoPipelineBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IIssueWebhook, IMessage
{
    private readonly GitHubUtils _gitHubUtils;
    private readonly ILogger<RepoPipelineBehavior<TMessage, TResponse>> _logger;

    public RepoPipelineBehavior(GitHubUtils gitHubUtils, ILogger<RepoPipelineBehavior<TMessage, TResponse>> logger)
    {
        _gitHubUtils = gitHubUtils;
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Preparing repo for message {Message}", message);
        await _gitHubUtils.EnsureRepoAsync(message.RepoPath, message.CloneUrl, message.RepoName, message.IssueNumber, cancellationToken);

        return await next(message, cancellationToken);
    }
}
