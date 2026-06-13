using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PersonalBot.Data.Models.Workflows;

public class ReactionCreated : Workflow
{
    public ReactionCreated(GitHubWebhook webhook)
    {
        ReactionContent = webhook.ReactionContent;
        IssueNumber = webhook.IssueNumber;
        RepoName = webhook.RepoName;
        CloneUrl = webhook.CloneUrl;
    }

    private ReactionCreated()
    {
        RepoName = string.Empty;
    }

    public string? ReactionContent { get; set; }
    public string? IssueNumber { get; set; }
    public string RepoName { get; set; }
    public string? CloneUrl { get; set; }
}
