using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalBot.Data;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class TaskApprovedHandler : INotificationHandler<TaskApproved>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskApprovedHandler> _logger;

    public TaskApprovedHandler(IServiceScopeFactory scopeFactory, ILogger<TaskApprovedHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async ValueTask Handle(TaskApproved notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plan approved for issue #{issueNum} — starting implementation", notification.IssueNumber);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();
        var existingSession = await dbContext.GetSessionFromIssueAsync(notification.IssueNumber, cancellationToken);

        string prompt =
            $"The plan for Issue #{notification.IssueNumber} has been approved. Create branch 'fix/issue-{notification.IssueNumber}', implement the code changes, run any available local tests, and raise a PR via `gh pr create`. Commit only changes related to this issue.";

        notification.ChildWorkflows.Add(
            new ChatStarted
            {
                RepoName = notification.RepoName,
                IssueNumber = notification.IssueNumber,
                Prompt = prompt,
                AgentPhase = AgentPhase.Execution,
                Session = existingSession,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );
    }
}
