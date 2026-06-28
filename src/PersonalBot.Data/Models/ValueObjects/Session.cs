namespace PersonalBot.Data.Models.ValueObjects;

public record Session
{
    public Session()
    {
        AgentName = "not initialized";
    }

    public Session(string agentName, IList<AgentMessage> messages)
    {
        AgentName = agentName;
        Messages = messages;
    }

    public string AgentName { get; init; }
    public IList<AgentMessage> Messages { get; init; } = [];
    public string? ConversationId { get; init; }
    public string? SerializedAgentSession { get; init; }
}
