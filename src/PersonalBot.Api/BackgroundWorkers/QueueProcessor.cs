namespace PersonalBot.Api.BackgroundWorkers;

public class QueueProcessor : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}