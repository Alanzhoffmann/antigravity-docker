namespace PersonalBot.Data.Models.ValueObjects;

public record Session(string AgentName, IList<AgentMessage> Messages, string? ConversationId = null, string? SerializedAgentSession = null);
