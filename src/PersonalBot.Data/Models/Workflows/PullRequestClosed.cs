namespace PersonalBot.Data.Models.Workflows;

public class PullRequestClosed : Workflow
{
    public PullRequestClosed(GitHubWebhook webhook)
    {
        PrMerged = webhook.PrMerged;
        HeadRef = webhook.HeadRef;
        RepoName = webhook.RepoName;
    }

    public bool PrMerged { get; set; }
    public string? HeadRef { get; set; }
    public string RepoName { get; set; }
}
