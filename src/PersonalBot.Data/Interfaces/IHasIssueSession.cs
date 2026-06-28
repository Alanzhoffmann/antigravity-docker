using PersonalBot.Data.Models.ValueObjects;

namespace PersonalBot.Data.Interfaces;

public interface IHasIssueSession
{
    Guid Id { get; }
    string IssueNumber { get; }
    Session? Session { get; set; }
}
