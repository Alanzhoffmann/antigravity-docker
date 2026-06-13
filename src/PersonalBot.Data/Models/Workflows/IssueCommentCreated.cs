namespace PersonalBot.Data.Models.Workflows;

public class IssueCommentCreated : Workflow
{
    public IssueCommentCreated(GitHubWebhook webhook)
    {
        IssueNumber = webhook.IssueNumber;
        CommentBody = webhook.CommentBody;
        RepoName = webhook.RepoName;
        CloneUrl = webhook.CloneUrl;
    }

    private IssueCommentCreated()
    {
        RepoName = string.Empty;
    }

    public string? IssueNumber { get; set; }
    public string? CommentBody { get; set; }
    public string RepoName { get; set; }
    public string? CloneUrl { get; set; }
}
