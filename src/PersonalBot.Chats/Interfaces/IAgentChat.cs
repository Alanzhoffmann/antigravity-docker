using PersonalBot.Chats.Models;
using PersonalBot.Data.Models;

namespace PersonalBot.Chats.Interfaces;

public interface IAgentChat
{
    bool IsEnabled { get; }
    string AgentName { get; }
    int SortOrder => 0; // Default sort order for agent chats, can be overridden by implementations
    Task<ChatResult> GetResponseAsync(AiTask aiTask, CancellationToken cancellationToken = default);
}

internal class NullChat : IAgentChat
{
    public bool IsEnabled => true;

    public int SortOrder => int.MaxValue; // Ensure NullChat is last in the order

    public string AgentName => nameof(NullChat);

    public Task<ChatResult> GetResponseAsync(AiTask aiTask, CancellationToken cancellationToken = default)
    {
        // TODO throw when retries are implemented
        return Task.FromResult(new ChatResult("No agent chat implementations are enabled. Please check the configuration.", aiTask.IssueNum, null));
    }
}
