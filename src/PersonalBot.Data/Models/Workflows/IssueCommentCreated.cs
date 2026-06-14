namespace PersonalBot.Data.Models.Workflows;

public class IssueCommentCreated : Workflow
{
    public string? IssueNumber { get; set; }
    public string? CommentBody { get; set; }
    public required string RepoName { get; set; }
    public string? CloneUrl { get; set; }
}
