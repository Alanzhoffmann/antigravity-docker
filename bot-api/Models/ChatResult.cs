namespace bot_api.Models;

public record ChatResult(string Output, string ConversationId, string? ArtifactOutput)
{
    public static implicit operator string(ChatResult result) => result.Output;
}
