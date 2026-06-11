using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Chats.Models;
using PersonalBot.Chats.Options;
using PersonalBot.Tools;
using PersonalBot.Tools.Factories;

namespace PersonalBot.Chats;

internal class OllamaChat : IAgentChat
{
    private readonly ILogger<OllamaChat> _logger;
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly ArtifactParser _artifactParser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, List<ChatMessage>> _conversationHistories = new();
    private readonly RepositoryToolsFactory _repositoryToolsFactory;
    private readonly RoslynAgentToolsFactory _roslynAgentToolsFactory;

    public OllamaChat(
        IOptionsMonitor<OllamaOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory,
        ArtifactParser artifactParser,
        RepositoryToolsFactory repositoryToolsFactory,
        RoslynAgentToolsFactory roslynAgentToolsFactory,
        ILogger<OllamaChat> logger
    )
    {
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _artifactParser = artifactParser;
        _logger = logger;
        _repositoryToolsFactory = repositoryToolsFactory;
        _roslynAgentToolsFactory = roslynAgentToolsFactory;
    }

    private string? Model => _optionsMonitor.CurrentValue.Model;
    private Uri? Url => _optionsMonitor.CurrentValue.Url;

    [MemberNotNullWhen(true, nameof(Model), nameof(Url))]
    public bool IsEnabled => Url is not null && !string.IsNullOrEmpty(Model);

    public int SortOrder => 1;

    public async Task<ChatResult> GetResponseAsync(
        string repoPath,
        string issueNum,
        string prompt,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            _logger.LogWarning(
                "OllamaChat is not enabled due to missing configuration. Model: '{Model}', Url: '{Url}'",
                Model,
                Url
            );
            return new ChatResult(
                "OllamaChat is not configured properly. Please check the logs for details.",
                issueNum,
                null
            );
        }

        var repositoryTools = await _repositoryToolsFactory.CreateAsync(repoPath);
        using var roslynAgentTools = await _roslynAgentToolsFactory.CreateAsync(repoPath);

        var client = _httpClientFactory.CreateClient(nameof(OllamaChat));
        using IChatClient ollamaClient = new OllamaApiClient(client, Model);
        var aiAgent = ollamaClient.AsAIAgent(
            instructions: """
            You are an autonomous .NET 11 developer agent. 
            You have access to Roslyn tools to navigate and edit the C# AST, and a raw Bash terminal.

            Guidelines:
            1. Use `RunBashCommand` to execute `git`, `ls`, `grep`, `dotnet test`, or any other terminal utilities.
            2. You can use pipes (|) and redirects (>) in your bash commands.
            3. If a bash command fails, read the error output and try again.
            4. To edit C#, prioritize using the Roslyn AST tools (FindReferences, ReplaceMethodCode) over standard bash text editors like sed or nano.
            5. When your task is complete, ensure you have committed and pushed your branch via bash.
            """,
            tools: [.. repositoryTools.Tools, .. roslynAgentTools.Tools]
        );

        // Start the conversation with context for the AI model
        if (!_conversationHistories.TryGetValue(issueNum, out var chatHistory))
        {
            chatHistory = [];
            _conversationHistories[issueNum] = chatHistory;
        }

        // Get user prompt and add to chat history
        _logger.LogInformation("User prompt for issue #{issueNum}: '{prompt}'", issueNum, prompt);
        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));

        // Stream the AI response and add to chat history
        _logger.LogInformation("Streaming response for issue #{issueNum}", issueNum);
        var response = "";
        await foreach (var item in aiAgent.RunStreamingAsync(chatHistory))
        {
            response += item.Text;
        }

        _logger.LogInformation(
            "Full response for issue #{issueNum}: '{response}'",
            issueNum,
            response
        );

        chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
        return new ChatResult(
            response,
            issueNum,
            await _artifactParser.TryReadPlanArtifact(repoPath)
        );
    }
}
