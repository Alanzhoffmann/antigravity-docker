using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class ReactionCreatedHandler : IRequestHandler<ReactionCreated>
{
    private readonly ILogger<ReactionCreatedHandler> _logger;

    public ReactionCreatedHandler(ILogger<ReactionCreatedHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<Unit> Handle(ReactionCreated request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reaction event: content='{ReactionContent}'", request.ReactionContent);
        if (request.ReactionContent != "+1" && request.ReactionContent != "👍")
        {
            return Unit.ValueTask;
        }

        if (string.IsNullOrEmpty(request.IssueNumber))
        {
            _logger.LogWarning($"reaction event missing issue.number");
            return Unit.ValueTask;
        }

        request.ChildWorkflows.Add(new TaskApproved { IssueNumber = request.IssueNumber, RepoName = request.RepoName });

        return Unit.ValueTask;
    }
}
