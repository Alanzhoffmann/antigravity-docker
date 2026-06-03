using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using bot_api.Models;

namespace bot_api.Chats;

public class AgyChat
{
    private readonly ILogger<AgyChat> _logger;

    public AgyChat(ILogger<AgyChat> logger)
    {
        _logger = logger;
    }

    public async Task<ChatResult> GetResponseAsync(string repoPath, string issueNum, string prompt)
    {
        string agentOutput = ExecuteAgyHeadless(repoPath, prompt);
        string newSessionId = ExtractConversationId(agentOutput);
        _logger.LogInformation($"[Processor] Issue #{issueNum} session: '{newSessionId}'");
        string cleanResponse = GetFinalResponseFromTranscript(newSessionId);
        if (string.IsNullOrEmpty(cleanResponse))
        {
            _logger.LogWarning($"[Processor] No transcript response, using raw output");
            cleanResponse = agentOutput;
        }

        // Embed the plan artifact content directly in the comment if available
        string planContent = TryReadPlanArtifact(newSessionId);
        if (!string.IsNullOrEmpty(planContent))
        {
            _logger.LogInformation(
                $"[Processor] Embedding plan artifact in comment for session '{newSessionId}'"
            );
            cleanResponse =
                $"{cleanResponse}\n\n---\n\n### 📋 Implementation Plan\n\n{planContent}";
        }
        else
        {
            _logger.LogWarning($"[Processor] No plan artifact found for session '{newSessionId}'");
        }

        return new ChatResult(cleanResponse, newSessionId);
    }

    private string ExecuteAgyHeadless(string repoPath, string prompt, string? conversationId = null)
    {
        _logger.LogInformation(
            $"[AgyRunner] Starting agy session='{conversationId ?? "new"}' cwd='{repoPath}'"
        );

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

        var sw = Stopwatch.StartNew();
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        sw.Stop();

        _logger.LogInformation(
            $"[AgyRunner] agy exited code={process.ExitCode} in {sw.Elapsed.TotalSeconds:F1}s"
        );

        if (!string.IsNullOrEmpty(error))
            _logger.LogWarning($"[AgyRunner] stderr: {error.Trim()}");

        if (process.ExitCode != 0)
        {
            _logger.LogError($"[AgyRunner] agy failed: {error.Trim()}");
            return $"Error executing agent: {error}";
        }

        return output;
    }

    string ExtractConversationId(string agyOutput)
    {
        var match = Regex.Match(
            agyOutput,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );
        string id = match.Success ? match.Value : string.Empty;
        _logger.LogInformation(
            $"[AgyRunner] Extracted conversation ID: '{(string.IsNullOrEmpty(id) ? "none found" : id)}'"
        );
        return id;
    }

    string GetFinalResponseFromTranscript(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return string.Empty;

        var transcriptPath =
            $"/root/.gemini/antigravity-cli/brain/{sessionId}/.system_generated/logs/transcript.jsonl";
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
                    if (
                        root.TryGetProperty("type", out var typeProp)
                        && typeProp.GetString() == "PLANNER_RESPONSE"
                    )
                    {
                        if (
                            root.TryGetProperty("content", out var contentProp)
                            && contentProp.ValueKind == JsonValueKind.String
                        )
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
            _logger.LogError(
                $"[Transcript] Error reading transcript for session '{sessionId}': {ex.Message}"
            );
        }

        _logger.LogInformation(
            $"[Transcript] Read {linesRead} lines, {responsesFound} planner responses (session='{sessionId}')"
        );
        return finalContent;
    }

    string TryReadPlanArtifact(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return string.Empty;
        string dir = $"/root/.gemini/antigravity-cli/brain/{sessionId}";
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning($"[Artifact] Artifact dir not found: '{dir}'");
            return string.Empty;
        }
        var candidates = Directory
            .GetFiles(dir, "implementation_plan.md", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly))
            .ToArray();
        if (candidates.Length == 0)
        {
            _logger.LogWarning($"[Artifact] No markdown artifacts in '{dir}'");
            return string.Empty;
        }
        try
        {
            string content = File.ReadAllText(candidates[0]);
            _logger.LogInformation(
                $"[Artifact] Read plan artifact '{candidates[0]}': {content.Length} chars"
            );
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Artifact] Failed reading artifact: {ex.Message}");
            return string.Empty;
        }
    }
}
