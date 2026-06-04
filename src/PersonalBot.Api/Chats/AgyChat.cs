using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PersonalBot.Api.Interfaces;
using PersonalBot.Api.Models;
using PersonalBot.Api.Options;

namespace PersonalBot.Api.Chats;

public class AgyChat : IAgentChat
{
    private readonly ILogger<AgyChat> _logger;
    private readonly IOptionsMonitor<AgyOptions> _optionsMonitor;
    private readonly ArtifactParser _artifactParser;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastExhaustedTokenTime = DateTime.MinValue;

    public AgyChat(IOptionsMonitor<AgyOptions> optionsMonitor, ArtifactParser artifactParser, TimeProvider timeProvider, ILogger<AgyChat> logger)
    {
        _optionsMonitor = optionsMonitor;
        _artifactParser = artifactParser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public bool IsEnabled => _optionsMonitor.CurrentValue.IsEnabled && (_lastExhaustedTokenTime - _timeProvider.GetUtcNow()).TotalSeconds > 600;

    public async Task<ChatResult> GetResponseAsync(string repoPath, string issueNum, string prompt, CancellationToken cancellationToken = default)
    {
        (string agentOutput, string? log) = await ExecuteAgyHeadless(repoPath, prompt);

        if (string.IsNullOrEmpty(agentOutput))
        {
            var hasExhaustedError = log?.Contains("RESOURCE_EXHAUSTED (code 429): Individual quota reached") ?? false;
            if (hasExhaustedError)
            {
                _lastExhaustedTokenTime = _timeProvider.GetUtcNow();
            }
        }

        string newSessionId = ExtractConversationId(agentOutput);
        _logger.LogInformation($"[Processor] Issue #{issueNum} session: '{newSessionId}'");
        string cleanResponse = GetFinalResponseFromTranscript(newSessionId);
        if (string.IsNullOrEmpty(cleanResponse))
        {
            _logger.LogWarning($"[Processor] No transcript response, using raw output");
            cleanResponse = agentOutput;
        }

        // Embed the plan artifact content directly in the comment if available
        string planContent = await TryReadPlanArtifact(newSessionId);
        if (string.IsNullOrEmpty(planContent))
        {
            _logger.LogWarning($"[Processor] No plan artifact found for session '{newSessionId}'");
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

        _logger.LogInformation($"[AgyRunner] Starting agy session='{conversationId ?? "new"}' cwd='{repoPath}'");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "agy",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        if (!string.IsNullOrEmpty(conversationId))
        {
            process.StartInfo.ArgumentList.Add("--conversation");
            process.StartInfo.ArgumentList.Add(conversationId);
        }

        process.StartInfo.ArgumentList.Add("-p");
        process.StartInfo.ArgumentList.Add(prompt);

        process.StartInfo.ArgumentList.Add("--log-file");
        process.StartInfo.ArgumentList.Add(logFileName);

        var sw = Stopwatch.StartNew();
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        sw.Stop();

        _logger.LogInformation($"[AgyRunner] agy exited code={process.ExitCode} in {sw.Elapsed.TotalSeconds:F1}s");

        if (!string.IsNullOrEmpty(error))
            _logger.LogWarning($"[AgyRunner] stderr: {error.Trim()}");

        string? logOutput = null;
        if (File.Exists(logFileName))
        {
            logOutput = await File.ReadAllTextAsync(logFileName, cancellationToken);
        }

        if (process.ExitCode != 0)
        {
            _logger.LogError($"[AgyRunner] agy failed: {error.Trim()}");
            return ($"Error executing agent: {error}", logOutput);
        }

        return (output, logOutput);
    }

    string ExtractConversationId(string agyOutput)
    {
        var match = Regex.Match(agyOutput, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        string id = match.Success ? match.Value : string.Empty;
        _logger.LogInformation($"[AgyRunner] Extracted conversation ID: '{(string.IsNullOrEmpty(id) ? "none found" : id)}'");
        return id;
    }

    string GetFinalResponseFromTranscript(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return string.Empty;

        var transcriptPath = $"/root/.gemini/antigravity-cli/brain/{sessionId}/.system_generated/logs/transcript.jsonl";
        _logger.LogInformation($"[Transcript] Reading transcript for session '{sessionId}'");

        if (!File.Exists(transcriptPath))
        {
            _logger.LogWarning($"[Transcript] Transcript not found at '{transcriptPath}'");
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
            _logger.LogError($"[Transcript] Error reading transcript for session '{sessionId}': {ex.Message}");
        }

        _logger.LogInformation($"[Transcript] Read {linesRead} lines, {responsesFound} planner responses (session='{sessionId}')");
        return finalContent;
    }

    private async Task<string> TryReadPlanArtifact(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return string.Empty;
        string dir = $"/root/.gemini/antigravity-cli/brain/{sessionId}";
        return await _artifactParser.TryReadPlanArtifact(dir);
    }
}
