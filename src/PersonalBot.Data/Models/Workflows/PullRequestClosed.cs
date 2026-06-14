namespace PersonalBot.Data.Models.Workflows;

public class PullRequestClosed : Workflow
{
    public bool PrMerged { get; set; }
    public string? HeadRef { get; set; }
    public required string RepoName { get; set; }
}
