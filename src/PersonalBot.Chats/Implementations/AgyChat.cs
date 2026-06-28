using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Chats.Models;
using PersonalBot.Chats.Options;
using PersonalBot.Data.Models.Enums;
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

    public string AgentName => nameof(AgyChat);

    public async ValueTask<ChatResult> GetResponseAsync(
        string repoPath,
        string prompt,
        AgentPhase phase = AgentPhase.Planning,
        string? session = null,
        CancellationToken cancellationToken = default
    )
    {
        (string agentOutput, string? log) = await ExecuteAgyHeadless(repoPath, prompt, cancellationToken: cancellationToken);

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
        string cleanResponse = GetFinalResponseFromTranscript(newSessionId);
        if (string.IsNullOrEmpty(cleanResponse))
        {
            _logger.LogWarning($"[Processor] No transcript response, using raw output");
            cleanResponse = agentOutput;
        }

        // Embed the plan artifact content directly in the comment if available
        string planContent = await TryReadPlanArtifactAsync(newSessionId, cancellationToken);
        if (string.IsNullOrEmpty(planContent))
        {
            _logger.LogWarning("[Processor] No plan artifact found for session '{newSessionId}'", newSessionId);
        }

        return new ChatResult(cleanResponse, newSessionId, planContent);
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
        _logger.LogInformation("[AgyRunner] Extracted conversation ID: '{ConversationId}'", string.IsNullOrEmpty(id) ? "none found" : id);
        return id;
    }

    string GetFinalResponseFromTranscript(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return string.Empty;

        var transcriptPath = $"/root/.gemini/antigravity-cli/brain/{sessionId}/.system_generated/logs/transcript.jsonl";

        _logger.LogInformation("[Transcript] Reading transcript for session '{sessionId}'", sessionId);

        if (!File.Exists(transcriptPath))
        {
            _logger.LogWarning("[Transcript] Transcript not found at '{transcriptPath}'", transcriptPath);
            return string.Empty;
        }

        string finalContent = string.Empty;
        int linesRead = 0,
            responsesFound = 0;
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
                    if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "PLANNER_RESPONSE")
                    {
                        if (root.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                        {
                            var content = contentProp.GetString();
                            if (!string.IsNullOrEmpty(content))
                            {
                                finalContent = content;
                                responsesFound++;
                            }
                        }
                    }
                }
                catch
                { /* Ignore malformed transcript lines */
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Transcript] Error reading transcript for session '{sessionId}': {ExceptionMessage}", sessionId, ex.Message);
        }

        _logger.LogInformation(
            "[Transcript] Read {linesRead} lines, {responsesFound} planner responses (session='{sessionId}')",
            linesRead,
            responsesFound,
            sessionId
        );
        return finalContent;
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
