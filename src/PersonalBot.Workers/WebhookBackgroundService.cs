using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PersonalBot.Workers;

internal class WebhookBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public WebhookBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var webhookProcessor = scope.ServiceProvider.GetRequiredService<WebhookProcessor>();

            await webhookProcessor.ProcessNextAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
