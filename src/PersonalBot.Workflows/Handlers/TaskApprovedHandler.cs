using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class TaskApprovedHandler : IRequestHandler<TaskApproved>
{
    private readonly ILogger<TaskApprovedHandler> _logger;

    public TaskApprovedHandler(ILogger<TaskApprovedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<Unit> Handle(TaskApproved request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plan approved for issue #{issueNum} — starting implementation", request.IssueNumber);

        string prompt =
            $"The plan for Issue #{request.IssueNumber} has been approved. Create branch 'fix/issue-{request.IssueNumber}', implement the code changes, run any available local tests, and raise a PR via `gh pr create`. Commit only changes related to this issue.";

        request.ChildWorkflows.Add(
            new ChatStarted
            {
                RepoName = request.RepoName,
                IssueNumber = request.IssueNumber,
                Prompt = prompt,
                AgentPhase = AgentPhase.Execution,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );

        return Unit.ValueTask;
    }
}
