using Microsoft.Extensions.Hosting;

namespace PersonalBot.Workers;

public class QueueProcessor : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}
