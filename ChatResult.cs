namespace bot_api;

public record ChatResult(string Response, string ConversationId)
{
    public static implicit operator string(ChatResult result) => result.Response;
}