using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class TaskApprovedHandler : INotificationHandler<TaskApproved>
{
    private readonly ILogger<TaskApprovedHandler> _logger;

    public TaskApprovedHandler(ILogger<TaskApprovedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(TaskApproved notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plan approved for issue #{issueNum} — starting implementation", notification.IssueNumber);

        string prompt =
            $"The plan for Issue #{notification.IssueNumber} has been approved. Create branch 'fix/issue-{notification.IssueNumber}', implement the code changes, run any available local tests, and raise a PR via `gh pr create`. Commit only changes related to this issue.";

        notification.ChildWorkflows.Add(
            new ChatStarted
            {
                RepoName = notification.RepoName,
                IssueNumber = notification.IssueNumber,
                Prompt = prompt,
                AgentPhase = AgentPhase.Execution,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );

        return ValueTask.CompletedTask;
    }
}
