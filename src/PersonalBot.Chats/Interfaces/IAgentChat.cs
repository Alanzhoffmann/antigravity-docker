using PersonalBot.Chats.Models;
using PersonalBot.Data.Models.Enums;

namespace PersonalBot.Chats.Interfaces;

public interface IAgentChat
{
    bool IsEnabled { get; }
    string AgentName { get; }
    int SortOrder => 0; // Default sort order for agent chats, can be overridden by implementations
    ValueTask<ChatResult> GetResponseAsync(
        string repoPath,
        string prompt,
        AgentPhase phase = AgentPhase.Planning,
        string? session = null,
        CancellationToken cancellationToken = default
    );
}

internal class NullChat : IAgentChat
{
    public bool IsEnabled => true;

    public int SortOrder => int.MaxValue; // Ensure NullChat is last in the order

    public string AgentName => nameof(NullChat);

    public ValueTask<ChatResult> GetResponseAsync(
        string repoPath,
        string prompt,
        AgentPhase phase = AgentPhase.Planning,
        string? session = null,
        CancellationToken cancellationToken = default
    ) => throw new InvalidOperationException("No agent chat implementations are enabled. Please check the configuration.");
}
