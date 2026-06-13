namespace PersonalBot.Data.Models.Workflows;

public class IssueOpened : Workflow
{
    public IssueOpened(GitHubWebhook webhook)
    {
        IssueNumber = webhook.IssueNumber;
        IssueTitle = webhook.IssueTitle;
        IssueBody = webhook.IssueBody;
        RepoName = webhook.RepoName;
        CloneUrl = webhook.CloneUrl;
    }

    private IssueOpened()
    {
        RepoName = string.Empty;
    }

    public string? IssueNumber { get; set; }
    public string? IssueTitle { get; set; }
    public string? IssueBody { get; set; }
    public string RepoName { get; set; }
    public string? CloneUrl { get; set; }
}
