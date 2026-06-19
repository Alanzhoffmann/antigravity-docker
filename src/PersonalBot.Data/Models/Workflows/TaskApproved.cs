using PersonalBot.Data.Interfaces;

namespace PersonalBot.Data.Models.Workflows;

public class TaskApproved : Workflow, IIsIssueWebhook
{
    public required string RepoName { get; set; }
    public required string IssueNumber { get; set; }
}
