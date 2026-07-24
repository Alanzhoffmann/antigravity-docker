using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Chats.Models;
using PersonalBot.Chats.Options;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.ValueObjects;
using PersonalBot.Tools;
using PersonalBot.Tools.Factories;
using PersonalBot.Utils;

namespace PersonalBot.Chats.Implementations;

internal class OllamaChat : IAgentChat
{
    private readonly ILogger<OllamaChat> _logger;
    private readonly IOptionsMonitor<OllamaOptions> _optionsMonitor;
    private readonly ArtifactParser _artifactParser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, List<ChatMessage>> _conversationHistories = [];
    private readonly RepositoryToolsFactory _repositoryToolsFactory;
    private readonly RoslynAgentToolsFactory _roslynAgentToolsFactory;
    private readonly GitHubUtils _gitHubUtils;

    public OllamaChat(
        IOptionsMonitor<OllamaOptions> optionsMonitor,
        IHttpClientFactory httpClientFactory,
        ArtifactParser artifactParser,
        RepositoryToolsFactory repositoryToolsFactory,
        RoslynAgentToolsFactory roslynAgentToolsFactory,
        GitHubUtils gitHubUtils,
        ILogger<OllamaChat> logger
    )
    {
        _optionsMonitor = optionsMonitor;
        _httpClientFactory = httpClientFactory;
        _artifactParser = artifactParser;
        _logger = logger;
        _repositoryToolsFactory = repositoryToolsFactory;
        _roslynAgentToolsFactory = roslynAgentToolsFactory;
        _gitHubUtils = gitHubUtils;
    }

    private string? Model => _optionsMonitor.CurrentValue.Model;
    private Uri? Url => _optionsMonitor.CurrentValue.Url;

    [MemberNotNullWhen(true, nameof(Model), nameof(Url))]
    public bool IsEnabled => Url is not null && !string.IsNullOrEmpty(Model);

    public int SortOrder => 1;

    public async ValueTask<ChatResult> GetResponseAsync(
        string repoPath,
        string prompt,
        AgentPhase phase = AgentPhase.Planning,
        Session? session = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("OllamaChat is not enabled due to missing configuration. Model: '{Model}', Url: '{Url}'", Model, Url);
            throw new InvalidOperationException("OllamaChat is not configured properly. Please check the logs for details.");
        }

        var repositoryTools = await _repositoryToolsFactory.CreateAsync(repoPath);
        using var roslynAgentTools = await _roslynAgentToolsFactory.CreateAsync(repoPath);

        var client = _httpClientFactory.CreateClient(nameof(OllamaChat));
        using IChatClient ollamaClient = new OllamaApiClient(client, Model);
        string systemInstructions = GetInstructions(phase);

        var aiAgent = ollamaClient.AsHarnessAgent(
            new HarnessAgentOptions
            {
                ChatOptions = new ChatOptions { Instructions = systemInstructions, Tools = [.. repositoryTools.Tools, .. roslynAgentTools.Tools] },
            }
        );

        AgentSession agentSession;
        if (session is not null && !string.IsNullOrEmpty(session.SerializedAgentSession))
        {
            var jsonState = JsonSerializer.Deserialize<JsonElement>(session.SerializedAgentSession);
            agentSession = await aiAgent.DeserializeSessionAsync(jsonState, cancellationToken: cancellationToken);
        }
        else
        {
            agentSession = await aiAgent.CreateSessionAsync(cancellationToken);
        }

        if (!agentSession.TryGetInMemoryChatHistory(out var chatHistory))
        {
            chatHistory = [];
            if (session is not null)
            {
                chatHistory = [.. session.Messages.Select(m => new ChatMessage(MapMessageTypeToRole(m.Type), m.Message))];
            }
        }

        chatHistory.Add(new ChatMessage(ChatRole.User, prompt));

        string response = await GetResponse(phase, repoPath, aiAgent, chatHistory, agentSession, cancellationToken);

        var serializedSession = await aiAgent.SerializeSessionAsync(agentSession, cancellationToken: cancellationToken);
        return new ChatResult(
            response,
            new Session(nameof(OllamaChat), [.. chatHistory.Select(m => new AgentMessage(m.Text, MapRoleToMessageType(m.Role)))])
            {
                SerializedAgentSession = serializedSession.ToString(),
            },
            await _artifactParser.TryReadPlanArtifact(repoPath, cancellationToken)
        );
    }

    private static MessageType MapRoleToMessageType(ChatRole role) =>
        role == ChatRole.Assistant ? MessageType.Assistant
        : role == ChatRole.User ? MessageType.User
        : role == ChatRole.System ? MessageType.System
        : role == ChatRole.Tool ? MessageType.Tool
        : MessageType.Unknown;

    private static ChatRole MapMessageTypeToRole(MessageType messageType) =>
        messageType switch
        {
            MessageType.Assistant => ChatRole.Assistant,
            MessageType.User => ChatRole.User,
            MessageType.Tool => ChatRole.Tool,
            MessageType.System => ChatRole.System,
            _ => ChatRole.System,
        };

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
        string repoPath,
        HarnessAgent aiAgent,
        List<ChatMessage> chatHistory,
        AgentSession agentSession,
        CancellationToken cancellationToken = default
    )
    {
        int maxAttempts = 3; // Allow the AI a few tries to get it right
        string response = string.Empty;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            StringBuilder output = new();

            // 1. Generate the response
            await foreach (var item in aiAgent.RunStreamingAsync(chatHistory, agentSession, cancellationToken: cancellationToken))
            {
                output.Append(item.Text);
            }

            response = output.ToString();

            // 2. Await the new async validation logic
            var (isValid, validationError) = await TryValidateOutputAsync(phase, response, repoPath, cancellationToken);

            if (isValid)
            {
                break; // Success! Exit the retry loop.
            }

            // 3. Handle Failure: Feed the error back to the AI
            _logger.LogWarning("Validation failed on attempt {Attempt}. Error: {Error}", attempt, validationError);

            if (attempt == maxAttempts)
            {
                // If we've exhausted our attempts, NOW we throw to the outer workflow.
                throw new InvalidOperationException($"AI failed to produce valid output after {maxAttempts} attempts. Last error: {validationError}");
            }

            // Inject the feedback into the chat history so the AI learns from its mistake
            // Note: The assistant's bad response was already added to the session by RunStreamingAsync.
            // We just need to add the user's correction prompt.
            string correctionPrompt =
                $"SYSTEM ERROR: Your previous output failed validation with the following error:\n{validationError}\n\nPlease correct the mistake and try again.";

            chatHistory.Add(new ChatMessage(ChatRole.System, correctionPrompt));

            // The loop restarts, calling RunStreamingAsync again with the updated history!
        }

        return response;
    }

    private async Task<(bool IsValid, string ErrorMessage)> TryValidateOutputAsync(
        AgentPhase phase,
        string response,
        string repoPath,
        CancellationToken cancellationToken
    )
    {
        if (phase == AgentPhase.Planning)
        {
            // Check if the AI actually created the file
            string planContent = await _artifactParser.TryReadPlanArtifact(repoPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(planContent))
            {
                return (
                    false,
                    "You failed to generate the 'implementation_plan.md' artifact. You must use your tools to write the markdown file to the workspace before completing your turn."
                );
            }
        }
        else if (phase == AgentPhase.Execution)
        {
            // 1. Check for lazy conversational text
            // if (response.Length < 100 && (response.Contains("I will") || response.Contains("working on")))
            // {
            //     return (false, "Do not use conversational text. You must use `RunBashCommand` or Roslyn tools immediately to execute the plan.");
            // }

            // 2. Check if commits were actually made using your new method
            int commitsAhead = await _gitHubUtils.GetCommitsAheadOfMainAsync(repoPath, "main", cancellationToken);

            if (commitsAhead == 0)
            {
                return (false, "Execution failed. No commits were added to the branch. You must modify the code and commit your changes using the bash tools.");
            }
        }

        return (true, string.Empty);
    }
}
