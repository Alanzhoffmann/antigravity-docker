using PersonalBot.Data.Models;

namespace PersonalBot.Data.Interfaces;

public interface IWebhookService
{
    Task<GitHubWebhook?> GetNextAsync(CancellationToken cancellationToken = default);
    Task AddNewAsync(GitHubWebhook webhook, CancellationToken cancellationToken = default);
}
