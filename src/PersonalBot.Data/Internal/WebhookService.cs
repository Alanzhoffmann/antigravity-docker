using Microsoft.EntityFrameworkCore;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models;

namespace PersonalBot.Data.Internal;

internal class WebhookService : IWebhookService
{
    private readonly BotDbContext _context;

    public WebhookService(BotDbContext context)
    {
        _context = context;
    }

    public void AddNew(GitHubWebhook webhook)
    {
        _context.Add(webhook);
    }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (!_context.MigrationState.IsDone)
        {
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<GitHubWebhook?> GetNextAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_context.MigrationState.IsDone)
        {
            return null;
        }

        return await _context
            .Webhooks.OrderBy(w => w.CreatedAt)
            .FirstOrDefaultAsync(
                w => w.Status == WebhookStatus.Pending,
                cancellationToken: cancellationToken
            );
    }
}
