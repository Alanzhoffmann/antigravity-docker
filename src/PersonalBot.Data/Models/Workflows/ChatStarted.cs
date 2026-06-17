using PersonalBot.Data.Models.Enums;

namespace PersonalBot.Data.Models.Workflows;

public class ChatStarted : Workflow
{
    public required string RepoName { get; set; }
    public required string RepoPath { get; set; }
    public required string IssueNumber { get; set; }
    public required string Prompt { get; set; }
    public required AgentPhase AgentPhase { get; init; }
    public string? ChatOutput { get; set; }
    public string? Session { get; set; }
    public string? ArtifactOutput { get; set; }
    public string? AgentName { get; set; }
}
