using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.Handlers;

public class FeedbackReceivedHandler : INotificationHandler<FeedbackReceived>
{
    private readonly ILogger<FeedbackReceived> _logger;

    public FeedbackReceivedHandler(ILogger<FeedbackReceived> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(FeedbackReceived notification, CancellationToken cancellationToken)
    {
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
                IssueNumber = notification.IssueNumber,
                Prompt = execPrompt,
                AgentPhase = AgentPhase.Planning,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );

        return ValueTask.CompletedTask;
    }
}
