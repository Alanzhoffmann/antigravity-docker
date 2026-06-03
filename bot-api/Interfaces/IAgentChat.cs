using bot_api.Models;

namespace bot_api.Interfaces;

public interface IAgentChat
{
    bool IsEnabled { get; }
    int SortOrder => 0; // Default sort order for agent chats, can be overridden by implementations
    Task<ChatResult> GetResponseAsync(string repoPath, string issueNum, string prompt);
}

public class NullChat : IAgentChat
{
    public bool IsEnabled => true;

    public int SortOrder => int.MaxValue; // Ensure NullChat is last in the order

    public Task<ChatResult> GetResponseAsync(string repoPath, string issueNum, string prompt)
    {
        return Task.FromResult(new ChatResult("No agent chat implementations are enabled. Please check the configuration.", issueNum));
    }
}
