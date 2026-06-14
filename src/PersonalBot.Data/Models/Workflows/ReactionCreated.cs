namespace PersonalBot.Data.Models.Workflows;

public class ReactionCreated : Workflow
{
    public string? ReactionContent { get; set; }
    public string? IssueNumber { get; set; }
    public required string RepoName { get; set; }
    public string? CloneUrl { get; set; }
}
