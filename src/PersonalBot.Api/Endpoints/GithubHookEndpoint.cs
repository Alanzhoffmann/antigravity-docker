using System.Text.Json;
using PersonalBot.Utils;

namespace PersonalBot.Api.Endpoints;

public static class GithubHookEndpoint
{
    public static void MapGithubHookEndpoint(this WebApplication app)
    {
        HashSet<string> supportedRepos = new HashSet<string> { "antigravity-docker" };
        // ─── WEBHOOK ENDPOINT ───────────────────────────────────────────────────────
        // Returns 202 immediately so GitHub never times out; real work runs in background.

        app.MapPost(
            "/github-webhook",
            async (WebhookProcessor webhookProcessor, HttpContext context, ILogger logger) =>
            {
                var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
                var deliveryId = context.Request.Headers["X-GitHub-Delivery"].ToString();
                logger.LogInformation("Received event='{EventType}' delivery='{DeliveryId}'", eventType, deliveryId);

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
                    logger.LogError(ex, "Failed to read request body: {Message}", ex.Message);
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
                    logger.LogError(ex, "Invalid JSON payload: {Message}", ex.Message);
                    return Results.BadRequest(new { error = "Invalid JSON payload" });
                }

                var repoName = root.GetNestedStringSafe("repository", "name");
                if (string.IsNullOrEmpty(repoName) || !supportedRepos.Contains(repoName))
                {
                    logger.LogInformation("Ignoring unsupported/missing repo: '{RepoName}'", repoName ?? "<null>");
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
                        logger.LogError(ex, "Unhandled exception: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
                    }
                });

                return Results.Accepted(value: new { status = "Accepted", delivery = deliveryId });
            }
        );
    }
}
