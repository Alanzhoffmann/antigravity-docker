using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class IssueOpenedHandler : IRequestHandler<IssueOpened>
{
    private readonly ILogger<IssueOpenedHandler> _logger;

    public IssueOpenedHandler(ILogger<IssueOpenedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<Unit> Handle(IssueOpened request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.IssueNumber))
        {
            _logger.LogWarning($"issues/opened missing issue.number");
            return Unit.ValueTask;
        }

        var title = request.IssueTitle ?? string.Empty;
        var body = request.IssueBody ?? string.Empty;

        string prompt =
            $@"Analyze Issue #{request.IssueNumber}: {title}

{body}

INSTRUCTIONS:
1. Formulate a detailed implementation plan.
2. Write it to an artifact file called 'implementation_plan.md' (ArtifactType=implementation_plan). This is mandatory.
3. Answer with a GitHub comment for issue #{request.IssueNumber} summarising the plan.
4. Ask for a 👍 reaction or 'approved' comment to proceed. Do NOT write any code yet.";

        request.ChildWorkflows.Add(
            new ChatStarted
            {
                RepoName = request.RepoName,
                IssueNumber = request.IssueNumber,
                Prompt = prompt,
                AgentPhase = AgentPhase.Planning,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );

        return Unit.ValueTask;
    }
}
