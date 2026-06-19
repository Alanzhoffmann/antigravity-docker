using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class TaskApprovedHandler : INotificationHandler<TaskApproved>
{
    private readonly GitHubUtils _gitHubUtils;
    private readonly IChatResolver _chatResolver;
    private readonly ILogger<TaskApprovedHandler> _logger;

    public TaskApprovedHandler(GitHubUtils gitHubUtils, IChatResolver chatResolver, ILogger<TaskApprovedHandler> logger)
    {
        _gitHubUtils = gitHubUtils;
        _chatResolver = chatResolver;
        _logger = logger;
    }

    public async ValueTask Handle(TaskApproved notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plan approved for issue #{issueNum} — starting implementation", notification.IssueNumber);

        string prompt =
            $"The plan for Issue #{notification.IssueNumber} has been approved. Create branch 'fix/issue-{notification.IssueNumber}', implement the code changes, run any available local tests, and raise a PR via `gh pr create`. Commit only changes related to this issue.";

        var agentChat = _chatResolver.ResolveCurrent();
        string response = await agentChat.GetResponseAsync(notification.RepoPath, prompt, AgentPhase.Execution, notification.Session, cancellationToken);

        await _gitHubUtils.PostGitHubCommentAsync(notification.RepoPath, notification.IssueNumber, response, string.Empty, cancellationToken);
        _logger.LogInformation("Implementation complete for issue #{issueNum}", notification.IssueNumber);
    }
}
