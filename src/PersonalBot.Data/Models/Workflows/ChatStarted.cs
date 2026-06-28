using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.ValueObjects;

namespace PersonalBot.Data.Models.Workflows;

public class ChatStarted : Workflow, IIsIssueWebhook, IHasIssueSession
{
    public required string RepoName { get; set; }
    public required string IssueNumber { get; set; }
    public required string Prompt { get; set; }
    public required AgentPhase AgentPhase { get; init; }
    public string? ChatOutput { get; set; }
    public Session? Session { get; set; }
    public string? ArtifactOutput { get; set; }
}
