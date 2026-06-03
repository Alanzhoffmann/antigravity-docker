using bot_api.Interfaces;
using bot_api.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace bot_api.Chats;

public class OllamaChat : IAgentChat
{
    private readonly ILogger<OllamaChat> _logger;

    private readonly Dictionary<string, List<ChatMessage>> _conversationHistories = new();

    public OllamaChat(ILogger<OllamaChat> logger)
    {
        _logger = logger;
    }

    public async Task<ChatResult> GetResponseAsync(string repoPath, string issueNum, string prompt)
    {
        IChatClient chatClient = new OllamaApiClient(new Uri("http://localhost:11434/"), "qwen3.5:9b");

        // Start the conversation with context for the AI model
        if (!_conversationHistories.TryGetValue(issueNum, out var chatHistory))
        {
            chatHistory = new List<ChatMessage>
            {
                new ChatMessage(
                    ChatRole.System,
                    "You are an assistant for a developer working on a GitHub issue. Provide helpful responses to their prompts based on the context of the issue and the repository."
                ),
            };
            _conversationHistories[issueNum] = chatHistory;
        }
        // Get user prompt and add to chat history
        _logger.LogInformation($"[OllamaChat] User prompt for issue #{issueNum}: '{prompt}'");
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));

        // Stream the AI response and add to chat history
        _logger.LogInformation($"[OllamaChat] Streaming response for issue #{issueNum}");
        var response = "";
        await foreach (ChatResponseUpdate item in chatClient.GetStreamingResponseAsync(chatHistory))
        {
            response += item.Text;
        }

        _logger.LogInformation($"[OllamaChat] Full response for issue #{issueNum}: '{response}'");

        chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
        return new ChatResult(response, issueNum);
    }
}
