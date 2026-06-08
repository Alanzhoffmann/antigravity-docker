using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalBot.Data.Interfaces;

namespace PersonalBot.Workers;

public class QueueProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public QueueProcessor(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var scope = _serviceScopeFactory.CreateScope();
            var taskQueryService = scope.ServiceProvider.GetRequiredService<ITaskQueryService>();

            await ProcessTasksAsync(taskQueryService, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessTasksAsync(
        ITaskQueryService taskQueryService,
        CancellationToken cancellationToken
    )
    {
        var nextTask = await taskQueryService.GetNextPendingTaskAsync(cancellationToken);
        if (nextTask is not null)
        {
            // Process the task here
        }
    }
}
