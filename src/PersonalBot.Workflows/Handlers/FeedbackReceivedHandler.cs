using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalBot.Data;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class FeedbackReceivedHandler : INotificationHandler<FeedbackReceived>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeedbackReceived> _logger;

    public FeedbackReceivedHandler(IServiceScopeFactory scopeFactory, ILogger<FeedbackReceived> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async ValueTask Handle(FeedbackReceived notification, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        var existingSession = await dbContext.GetSessionFromIssueAsync(notification.IssueNumber, cancellationToken);

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
                RepoName = notification.RepoName,
                RepoPath = notification.RepoPath,
                IssueNumber = notification.IssueNumber,
                Prompt = execPrompt,
                AgentPhase = AgentPhase.Planning,
                Session = existingSession,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );
    }
}
