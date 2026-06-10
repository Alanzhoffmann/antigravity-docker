using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PersonalBot.Api;
using PersonalBot.Api.Chats;
using PersonalBot.Api.Interfaces;
using PersonalBot.Api.Options;
using PersonalBot.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddHttpClient(
    nameof(OllamaChat),
    (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<OllamaOptions>>().CurrentValue;
        client.BaseAddress = options.Url ?? throw new InvalidOperationException("Ollama url is missing");
        client.Timeout = Timeout.InfiniteTimeSpan;
    }
);

builder.Services.AddSingleton<IAgentChat, OllamaChat>();
builder.Services.AddSingleton<IAgentChat, AgyChat>();
builder.Services.AddSingleton<IAgentChat, NullChat>();
builder.Services.AddScoped<WebhookProcessor>();
builder.Services.AddPersonalBotTools();

builder.Services.AddOptions<OllamaOptions>().BindConfiguration(OllamaOptions.SectionName);
builder.Services.AddOptions<AgyOptions>().BindConfiguration(AgyOptions.SectionName);

var app = builder.Build();

// ─── CONFIGURATION ────────────────────────────────────────────────────────────

HashSet<string> supportedRepos = new HashSet<string> { "antigravity-docker" };

// ─── LOGGING HELPERS ──────────────────────────────────────────────────────────

void Log(string level, string component, string message)
{
    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z] [{level}] [{component}] {message}");
}

void LogInfo(string component, string message) => Log("INFO ", component, message);
void LogError(string component, string message) => Log("ERROR", component, message);

// ─── WEBHOOK ENDPOINT ───────────────────────────────────────────────────────
// Returns 202 immediately so GitHub never times out; real work runs in background.

app.MapPost(
    "/github-webhook",
    async (WebhookProcessor webhookProcessor, HttpContext context) =>
    {
        var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
        var deliveryId = context.Request.Headers["X-GitHub-Delivery"].ToString();
        LogInfo("Webhook", $"Received event='{eventType}' delivery='{deliveryId}'");

        // Buffer the full body before doing anything — the stream is disposed once
        // we return the HTTP response, so the background task needs its own copy.
        string rawBody;
        try
        {
            using var reader = new StreamReader(context.Request.Body);
            rawBody = await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            LogError("Webhook", $"Failed to read request body: {ex.Message}");
            return Results.BadRequest(new { error = "Failed to read body" });
        }

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            root = doc.RootElement.Clone(); // Clone so we can use after doc is disposed
        }
        catch (JsonException ex)
        {
            LogError("Webhook", $"Invalid JSON payload: {ex.Message}");
            return Results.BadRequest(new { error = "Invalid JSON payload" });
        }

        var repoName = root.GetNestedStringSafe("repository", "name");
        if (string.IsNullOrEmpty(repoName) || !supportedRepos.Contains(repoName))
        {
            LogInfo("Webhook", $"Ignoring unsupported/missing repo: '{repoName}'");
            return Results.Ok(new { status = "Ignored: unsupported repository" });
        }

        // Fire-and-forget — GitHub gets 202 in milliseconds, no timeout risk.
        _ = Task.Run(async () =>
        {
            try
            {
                await webhookProcessor.ProcessWebhookAsync(eventType, deliveryId, rawBody, repoName);
            }
            catch (Exception ex)
            {
                LogError("Processor", $"Unhandled exception: {ex.Message}\n{ex.StackTrace}");
            }
        });

        return Results.Accepted(value: new { status = "Accepted", delivery = deliveryId });
    }
);

app.Run("http://0.0.0.0:8080");

public static class JsonElementExtensions
{
    public static string? GetStringSafe(this JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var prop))
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        return null;
    }

    public static string? GetNestedStringSafe(this JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
                return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}
