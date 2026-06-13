namespace PersonalBot.Data.Models.Workflows;

public class TaskApproved : Workflow
{
    public string? SessionId { get; set; }
    public required string IssueNumber { get; set; }
    public required string RepoPath { get; set; }
}
