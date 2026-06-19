using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class IssueOpenedHandler : INotificationHandler<IssueOpened>
{
    private readonly ILogger<IssueOpenedHandler> _logger;

    public IssueOpenedHandler(ILogger<IssueOpenedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(IssueOpened notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(notification.IssueNumber))
        {
            _logger.LogWarning($"issues/opened missing issue.number");
            return ValueTask.CompletedTask;
        }

        var title = notification.IssueTitle ?? string.Empty;
        var body = notification.IssueBody ?? string.Empty;

        string prompt =
            $@"Analyze Issue #{notification.IssueNumber}: {title}

{body}

INSTRUCTIONS:
1. Formulate a detailed implementation plan.
2. Write it to an artifact file called 'implementation_plan.md' (ArtifactType=implementation_plan). This is mandatory.
3. Answer with a GitHub comment for issue #{notification.IssueNumber} summarising the plan.
4. Ask for a 👍 reaction or 'approved' comment to proceed. Do NOT write any code yet.";

        notification.ChildWorkflows.Add(
            new ChatStarted
            {
                RepoName = notification.RepoName,
                IssueNumber = notification.IssueNumber,
                Prompt = prompt,
                AgentPhase = AgentPhase.Planning,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );

        return ValueTask.CompletedTask;
    }
}
