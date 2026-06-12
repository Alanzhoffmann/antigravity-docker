using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using PersonalBot.Chats.Enums;
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
    private readonly Dictionary<string, List<ChatMessage>> _conversationHistories = [];
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
        AgentPhase phase = AgentPhase.Planning,
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
        string systemInstructions = GetInstructions(phase);

        // Security: Only give the execution agent the ability to write code and run bash
        IList<AITool> allowedTools =
            phase is AgentPhase.Planning
                ? [.. repositoryTools.ReadOnlyTools, .. roslynAgentTools.ReadOnlyTools] // Read-only tools
                : [.. repositoryTools.Tools, .. roslynAgentTools.Tools]; // Full read/write suite

        var aiAgent = ollamaClient.AsAIAgent(instructions: systemInstructions, tools: allowedTools);

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
        string response = await GetResponse(phase, aiAgent, chatHistory, cancellationToken);

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

    private static string GetInstructions(AgentPhase phase) =>
        phase switch
        {
            AgentPhase.Planning => """
                You are an elite .NET 11 Software Architect.
                Your goal is to investigate issues and write a step-by-step implementation plan.
                1. Use your tools to read the necessary files and find references.
                2. DO NOT write code or modify files. 
                3. Output a detailed markdown plan explaining exactly which files need to change and the logic required.
                4. End your message by asking the user to approve the plan.
                """,

            AgentPhase.Execution => """
                You are a silent, autonomous .NET 11 execution engine.
                You will be given an approved implementation plan.
                CRITICAL RULES:
                1. NEVER output conversational text (e.g., "I will work on it", "Let's begin").
                2. IMMEDIATELY call your tools to execute the plan.
                3. Use `RunBashCommand` to create branches, commit, and push.
                4. Use Roslyn tools to modify the C# code safely.
                5. DO NOT output standard text until the branch is successfully pushed.
                """,

            _ => throw new InvalidOperationException($"{phase} is not a valid AgentPhase"),
        };

    private async Task<string> GetResponse(
        AgentPhase phase,
        Microsoft.Agents.AI.ChatClientAgent aiAgent,
        List<ChatMessage> chatHistory,
        CancellationToken cancellationToken = default
    )
    {
        int maxAttempts = phase is AgentPhase.Execution ? 2 : 1;
        StringBuilder output = new();
        string response = string.Empty;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            output = output.Clear();
            await foreach (
                var item in aiAgent.RunStreamingAsync(
                    chatHistory,
                    cancellationToken: cancellationToken
                )
            )
            {
                output.Append(item.Text);
            }

            response = output.ToString();

            // --- EXECUTION SAFETY NET ---
            if (phase == AgentPhase.Execution)
            {
                if (
                    response.Length < 100
                    && (response.Contains("I will") || response.Contains("working on"))
                )
                {
                    _logger.LogWarning("Execution agent gave a lazy response. Forcing correction.");

                    chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
                    chatHistory.Add(
                        new ChatMessage(
                            ChatRole.User,
                            "SYSTEM ERROR: Do not converse. Use `RunBashCommand` or Roslyn tools immediately to execute the plan."
                        )
                    );

                    continue;
                }
            }

            break;
        }

        return response;
    }
}
