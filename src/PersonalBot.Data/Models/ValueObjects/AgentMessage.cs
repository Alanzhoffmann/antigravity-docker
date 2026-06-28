using PersonalBot.Data.Models.Enums;

namespace PersonalBot.Data.Models.ValueObjects;

public record AgentMessage(string Message, MessageType Type);
