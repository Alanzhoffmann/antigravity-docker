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

    public async Task AddNewAsync(
        GitHubWebhook webhook,
        CancellationToken cancellationToken = default
    )
    {
        if (_context.MigrationState.IsDone)
        {
            _context.Add(webhook);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<GitHubWebhook?> GetNextAsync(CancellationToken cancellationToken = default)
    {
        if (!_context.MigrationState.IsDone)
        {
            return null;
        }

        return await _context
            .Webhooks.OrderBy(w => w.CreatedAt)
            .FirstOrDefaultAsync(w => !w.IsProcessed, cancellationToken: cancellationToken);
    }
}
