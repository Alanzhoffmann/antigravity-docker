using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workflows.Handlers;

public class IssueOpenedHandler : INotificationHandler<IssueOpened>
{
    private readonly GitHubUtils _gitHubUtils;
    private readonly ILogger<IssueOpenedHandler> _logger;

    public IssueOpenedHandler(GitHubUtils gitHubUtils, ILogger<IssueOpenedHandler> logger)
    {
        _gitHubUtils = gitHubUtils;
        _logger = logger;
    }

    public async ValueTask Handle(IssueOpened notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(notification.IssueNumber))
        {
            _logger.LogWarning($"issues/opened missing issue.number");
            return;
        }

        // Each issue gets its own isolated clone so branches and commits never bleed across issues
        string localRepoPath = GitHubUtils.GetIssueRepoPath(notification.RepoName, notification.IssueNumber);
        await _gitHubUtils.EnsureRepoAsync(localRepoPath, notification.CloneUrl, notification.RepoName, notification.IssueNumber, cancellationToken);

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
                RepoPath = localRepoPath,
                IssueNumber = notification.IssueNumber,
                Prompt = prompt,
                AgentPhase = AgentPhase.Planning,
                ChildWorkflows = [new ChatIssueReply()],
            }
        );
    }
}
