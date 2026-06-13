using PersonalBot.Data.Models;

namespace PersonalBot.Data.Interfaces;

public interface IWebhookService
{
    ValueTask<GitHubWebhook?> GetNextAsync(CancellationToken cancellationToken = default);
    void AddNew(GitHubWebhook webhook);
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}
