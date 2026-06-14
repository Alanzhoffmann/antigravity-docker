namespace PersonalBot.Data.Models.Workflows;

public class IssueOpened : Workflow
{
    public string? IssueNumber { get; set; }
    public string? IssueTitle { get; set; }
    public string? IssueBody { get; set; }
    public required string RepoName { get; set; }
    public string? CloneUrl { get; set; }
}
