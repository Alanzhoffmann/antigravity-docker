namespace PersonalBot.Data.Models;

public class AiTask
{
    public Guid Id { get; set; }
    public string RepoPath { get; set; }
    public string IssueNum { get; set; }
    public string Prompt { get; set; }
    public string? SessionId { get; set; }
    public AiTaskStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int RetryCount { get; set; }
}

public enum AiTaskStatus
{
    Pending,
    Failed,
    Completed,
}
