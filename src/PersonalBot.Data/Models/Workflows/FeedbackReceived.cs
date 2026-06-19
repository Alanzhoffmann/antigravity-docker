using PersonalBot.Data.Interfaces;

namespace PersonalBot.Data.Models.Workflows;

public class FeedbackReceived : Workflow, IIsIssueWebhook
{
    public required string IssueNumber { get; set; }
    public required string CommentBody { get; set; }
    public required string RepoName { get; set; }
}
