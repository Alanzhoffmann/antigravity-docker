namespace PersonalBot.Data.Models.Workflows;

public class TaskApproved : Workflow
{
    public string? Session { get; set; }
    public required string IssueNumber { get; set; }
    public required string RepoPath { get; set; }
}
