namespace PersonalBot.Chats.Models;

public record ChatResult(string Output, string? Session, string? ArtifactOutput)
{
    public static implicit operator string(ChatResult result) => result.Output;
}
