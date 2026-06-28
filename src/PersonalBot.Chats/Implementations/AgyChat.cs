using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Chats.Models;
using PersonalBot.Chats.Options;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.ValueObjects;
using PersonalBot.Tools;
using PersonalBot.Utils;

namespace PersonalBot.Chats.Implementations;

internal partial class AgyChat : IAgentChat
{
    private readonly ILogger<AgyChat> _logger;
    private readonly IOptionsMonitor<AgyOptions> _optionsMonitor;
    private readonly ArtifactParser _artifactParser;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastExhaustedTokenTime = DateTimeOffset.MinValue;
    private readonly ProcessUtils _processUtils;

    public AgyChat(
        IOptionsMonitor<AgyOptions> optionsMonitor,
        ArtifactParser artifactParser,
        TimeProvider timeProvider,
        ILogger<AgyChat> logger,
        ProcessUtils processUtils
    )
    {
        _optionsMonitor = optionsMonitor;
        _artifactParser = artifactParser;
        _timeProvider = timeProvider;
        _logger = logger;
        _processUtils = processUtils;
    }

    public bool IsEnabled => _optionsMonitor.CurrentValue.IsEnabled && _timeProvider.GetUtcNow() > _lastExhaustedTokenTime.AddSeconds(600);

    public async ValueTask<ChatResult> GetResponseAsync(
        string repoPath,
        string prompt,
        AgentPhase phase = AgentPhase.Planning,
        Session? session = null,
        CancellationToken cancellationToken = default
    )
    {
        (string agentOutput, string? log) = await ExecuteAgyHeadless(repoPath, prompt, session?.ConversationId, cancellationToken: cancellationToken);

        if (string.IsNullOrEmpty(agentOutput))
        {
            var hasExhaustedError = log?.Contains("RESOURCE_EXHAUSTED (code 429): Individual quota reached") ?? false;
            if (hasExhaustedError)
            {
                _logger.LogWarning("Exhausted tokens for AgyChat");
                _lastExhaustedTokenTime = _timeProvider.GetUtcNow();
            }
        }

        string newSessionId = ExtractConversationId(agentOutput);
        var messages = GetMessagesFromTranscript(newSessionId);
        string? cleanResponse = messages.LastOrDefault(m => m.Type is MessageType.Assistant)?.Message;
        if (string.IsNullOrEmpty(cleanResponse))
        {
            _logger.LogWarning($"No transcript response, using raw output");
            cleanResponse = agentOutput;
        }

        // Embed the plan artifact content directly in the comment if available
        string planContent = await TryReadPlanArtifactAsync(newSessionId, cancellationToken);
        if (string.IsNullOrEmpty(planContent))
        {
            _logger.LogWarning("No plan artifact found for session '{newSessionId}'", newSessionId);
        }

        return new ChatResult(cleanResponse, new Session(nameof(AgyChat), messages, ConversationId: newSessionId), planContent);
    }

    private async Task<(string output, string? log)> ExecuteAgyHeadless(
        string repoPath,
        string prompt,
        string? conversationId = null,
        CancellationToken cancellationToken = default
    )
    {
        const string logFileName = "agy-logging.log";
        if (File.Exists(logFileName))
        {
            _logger.LogInformation("Deleting existing log file");
            File.Delete(logFileName);
        }

        var arguments = new List<string>();
        if (!string.IsNullOrEmpty(conversationId))
        {
            arguments.Add("--conversation");
            arguments.Add(conversationId);
        }

        arguments.Add("--prompt");
        arguments.Add($"\"{prompt}\"");
        arguments.Add("--log-file");
        arguments.Add(logFileName);

        var output = await _processUtils.RunProcessAsync("agy", arguments, repoPath, cancellationToken);

        string? logOutput = null;
        if (File.Exists(logFileName))
        {
            logOutput = await File.ReadAllTextAsync(logFileName, cancellationToken);
        }

        return (output, logOutput);
    }

    string ExtractConversationId(string agyOutput)
    {
        var match = ConversationIdRegex.Match(agyOutput);
        string id = match.Success ? match.Value : string.Empty;
        _logger.LogInformation("Extracted conversation ID: '{ConversationId}'", string.IsNullOrEmpty(id) ? "none found" : id);
        return id;
    }

    public List<AgentMessage> GetMessagesFromTranscript(string sessionId)
    {
        var messages = new List<AgentMessage>();

        if (string.IsNullOrEmpty(sessionId))
            return messages;

        var transcriptPath = $"/root/.gemini/antigravity-cli/brain/{sessionId}/.system_generated/logs/transcript.jsonl";

        _logger.LogInformation("Reading transcript for session '{sessionId}'", sessionId);

        if (!File.Exists(transcriptPath))
        {
            _logger.LogWarning("Transcript not found at '{transcriptPath}'", transcriptPath);
            return messages;
        }

        int linesRead = 0;

        try
        {
            foreach (var line in File.ReadLines(transcriptPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                linesRead++;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    // 1. Get the Event Type
                    if (root.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                    {
                        string eventType = typeProp.GetString() ?? string.Empty;

                        // 2. Map the Agy Event Type to your MessageType Enum
                        if (TryMapMessageType(eventType, out MessageType role))
                        {
                            // 3. Extract the content
                            string content = string.Empty;

                            // Most messages will have a simple string 'content' property
                            if (root.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                            {
                                content = contentProp.GetString() ?? string.Empty;
                            }
                            // Fallback for Tool Calls or Complex System Messages that might serialize differently
                            else if (root.TryGetProperty("args", out var argsProp) || root.TryGetProperty("result", out var resultProp))
                            {
                                content = root.GetRawText(); // Capture the raw JSON if it's a complex tool object
                            }

                            if (!string.IsNullOrEmpty(content))
                            {
                                messages.Add(new AgentMessage(content, role));
                            }
                        }
                    }
                }
                catch
                {
                    /* Ignore malformed transcript lines */
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading transcript for session '{sessionId}': {ExceptionMessage}", sessionId, ex.Message);
        }

        _logger.LogInformation("Read {linesRead} lines, generated {messageCount} chat messages (session='{sessionId}')", linesRead, messages.Count, sessionId);

        return messages;
    }

    private static bool TryMapMessageType(string agyEventType, out MessageType messageType)
    {
        // NOTE: You will need to verify the exact string values in your Agy .jsonl files.
        // These are the most common patterns for agent architectures.
        switch (agyEventType.ToUpperInvariant())
        {
            case "USER_PROMPT":
            case "USER_INPUT":
                messageType = MessageType.User;
                return true;

            case "PLANNER_RESPONSE":
            case "EXECUTION_RESPONSE":
            case "AGENT_MESSAGE":
                messageType = MessageType.Assistant;
                return true;

            case "SYSTEM_PROMPT":
            case "SYSTEM_MESSAGE":
            case "ERROR":
                messageType = MessageType.System;
                return true;

            case "TOOL_CALL":
            case "TOOL_RESULT":
            case "ACTION":
                messageType = MessageType.Tool;
                return true;

            default:
                // Ignore unrecognized event types (e.g., internal telemetry, debug logs)
                messageType = MessageType.Unknown;
                return false;
        }
    }

    private async Task<string> TryReadPlanArtifactAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            return string.Empty;
        string dir = $"/root/.gemini/antigravity-cli/brain/{sessionId}";
        return await _artifactParser.TryReadPlanArtifact(dir, cancellationToken);
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex ConversationIdRegex { get; }
}
