using PersonalBot.Data.Models.Enums;

namespace PersonalBot.Data.Models;

public class AiTask
{
    public Guid Id { get; set; }
    public required string RepoPath { get; set; }
    public required string IssueNum { get; set; }
    public required string Prompt { get; set; }
    public required AgentPhase Phase { get; init; }
    public string? Session { get; set; }
    public string? Agent { get; set; }

    public AiTaskStatus Status { get; set; } = AiTaskStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public int RetryCount { get; set; }
}
