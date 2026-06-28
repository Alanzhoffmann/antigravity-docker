using PersonalBot.Data.Models.ValueObjects;

namespace PersonalBot.Chats.Models;

public record ChatResult(string Output, Session? Session, string? ArtifactOutput)
{
    public static implicit operator string(ChatResult result) => result.Output;
}
