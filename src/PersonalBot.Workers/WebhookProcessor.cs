using Mediator;
using Microsoft.Extensions.Logging;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;
using PersonalBot.Utils;

namespace PersonalBot.Workers;

public partial class WebhookProcessor
{
    private readonly ILogger<WebhookProcessor> _logger;
    private readonly IWebhookService _webhookService;
    private readonly GitHubUtils _gitHubUtils;
    private readonly IPublisher _publisher;
    private readonly IChatResolver _chatResolver;

    public WebhookProcessor(
        ILogger<WebhookProcessor> logger,
        IWebhookService webhookService,
        IChatResolver chatResolver,
        GitHubUtils gitHubUtils,
        IPublisher publisher
    )
    {
        _logger = logger;
        _webhookService = webhookService;
        _gitHubUtils = gitHubUtils;
        _publisher = publisher;
        _chatResolver = chatResolver;
    }

    internal async ValueTask ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var webhook = await _webhookService.GetNextAsync(cancellationToken);
        if (webhook is null)
        {
            return;
        }
        try
        {
            _logger.LogInformation("Processing {Webhook}", webhook);

            webhook.Status = WebhookStatus.Running;
            await _webhookService.CommitAsync(cancellationToken);

            switch (webhook)
            {
                case { EventType: "issues", Action: "opened" }:
                    await _publisher.Publish(new IssueOpened(webhook), cancellationToken);
                    break;
                case { EventType: "reaction", Action: "created" }:
                    await _publisher.Publish(new ReactionCreated(webhook), cancellationToken);
                    break;
                case { EventType: "issue_comment", Action: "created" }:
                    await _publisher.Publish(new IssueCommentCreated(webhook), cancellationToken);
                    break;
                case { EventType: "pull_request", Action: "closed" }:
                    await _publisher.Publish(new PullRequestClosed(webhook), cancellationToken);
                    break;
                default:
                    _logger.LogInformation("Unhandled event='{eventType}' action='{action}' — no-op", webhook.EventType, webhook.Action);
                    break;
            }
            webhook.Status = WebhookStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error when processing {Webhook}", webhook);
            webhook.Status = WebhookStatus.Failed;
        }
        finally
        {
            await _webhookService.CommitAsync(cancellationToken);
        }
    }
}
