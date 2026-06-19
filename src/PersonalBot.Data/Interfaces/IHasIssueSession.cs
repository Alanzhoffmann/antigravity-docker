namespace PersonalBot.Data.Interfaces;

public interface IHasIssueSession
{
    string IssueNumber { get; }
    string? Session { get; set; }
}
