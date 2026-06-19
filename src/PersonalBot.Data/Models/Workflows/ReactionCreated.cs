using PersonalBot.Data.Interfaces;

namespace PersonalBot.Data.Models.Workflows;

public class ReactionCreated : Workflow, IIssueWebhook
{
    public string? ReactionContent { get; set; }
    public required string IssueNumber { get; set; }
    public required string RepoName { get; set; }
    public string? CloneUrl { get; set; }
}
