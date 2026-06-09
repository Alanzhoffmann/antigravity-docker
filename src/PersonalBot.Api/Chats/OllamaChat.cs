using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using PersonalBot.Api.Interfaces;
using PersonalBot.Api.Models;
using PersonalBot.Api.Options;
using PersonalBot.Tools;

namespace PersonalBot.Api.Chats;

public class OllamaChat : IAgentChat
{
    private readonly ILogger<OllamaChat> _logger;
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly ArtifactParser _artifactParser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, List<ChatMessage>> _conversationHistories = new();
    private readonly RepositoryTools _repositoryTools;

    public OllamaChat(
        IOptionsMonitor<OllamaOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory,
        ArtifactParser artifactParser,
        RepositoryTools repositoryTools,
        ILogger<OllamaChat> logger
    )
    {
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _artifactParser = artifactParser;
        _logger = logger;
        _repositoryTools = repositoryTools;
    }

    private string? Model => _optionsMonitor.CurrentValue.Model;
    private Uri? Url => _optionsMonitor.CurrentValue.Url;

    [MemberNotNullWhen(true, nameof(Model), nameof(Url))]
    public bool IsEnabled => Url is not null && !string.IsNullOrEmpty(Model);

    public int SortOrder => 1;

    public async Task<ChatResult> GetResponseAsync(string repoPath, string issueNum, string prompt, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning($"[OllamaChat] OllamaChat is not enabled due to missing configuration. Model: '{Model}', Url: '{Url}'");
            return new ChatResult("OllamaChat is not configured properly. Please check the logs for details.", issueNum, null);
        }

        _repositoryTools.SetRepoPath(repoPath);

        var client = _httpClientFactory.CreateClient(nameof(OllamaChat));
        using IChatClient ollamaClient = new OllamaApiClient(client, Model);
        var aiAgent = ollamaClient.AsAIAgent(
            instructions: "You are an assistant for a developer working on a GitHub issue. Provide helpful responses to their prompts based on the context of the issue and the repository.",
            tools: [.. _repositoryTools.Tools]
        );

        // Start the conversation with context for the AI model
        if (!_conversationHistories.TryGetValue(issueNum, out var chatHistory))
        {
            chatHistory = [];
            _conversationHistories[issueNum] = chatHistory;
        }

        // Get user prompt and add to chat history
        _logger.LogInformation($"[OllamaChat] User prompt for issue #{issueNum}: '{prompt}'");
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));

        // Stream the AI response and add to chat history
        _logger.LogInformation($"[OllamaChat] Streaming response for issue #{issueNum}");
        var response = "";
        await foreach (var item in aiAgent.RunStreamingAsync(chatHistory))
        {
            response += item.Text;
        }

        _logger.LogInformation($"[OllamaChat] Full response for issue #{issueNum}: '{response}'");

        chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
        return new ChatResult(response, issueNum, await _artifactParser.TryReadPlanArtifact(repoPath));
    }
}
