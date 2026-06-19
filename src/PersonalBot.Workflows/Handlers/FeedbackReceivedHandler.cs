using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class FeedbackReceivedHandler : IRequestHandler<FeedbackReceived>
{
    private readonly ILogger<FeedbackReceived> _logger;

    public FeedbackReceivedHandler(ILogger<FeedbackReceived> logger)
    {
        _logger = logger;
    }

    public ValueTask<Unit> Handle(FeedbackReceived request, CancellationToken cancellationToken)
    {
        string execPrompt =
            $"Feedback received on Issue #{request.IssueNumber}: '{request.CommentBody}'. Update the plan accordingly. Post an updated plan artifact and ask for another 👍 to proceed.";

        _logger.LogInformation(
            "Feedback on #{issueNum}: '{CommentBody}'",
            request.IssueNumber,
            request.CommentBody[..Math.Min(80, request.CommentBody.Length)]
        );

        request.ChildWorkflows.Add(
            new ChatStarted
            {
                RepoName = request.RepoName,
                IssueNumber = request.IssueNumber,
                Prompt = execPrompt,
                AgentPhase = AgentPhase.Planning,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );

        return Unit.ValueTask;
    }
}
