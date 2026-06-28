namespace PersonalBot.Data.Interfaces;

public interface IHasIssueSession
{
    Guid Id { get; }
    string IssueNumber { get; }
    string? Session { get; set; }
    string? AgentName { get; }
}
