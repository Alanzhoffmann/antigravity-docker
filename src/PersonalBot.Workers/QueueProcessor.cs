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
            var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();

            await ProcessTasksAsync(taskService, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessTasksAsync(
        ITaskService taskService,
        CancellationToken cancellationToken
    )
    {
        var nextTask = await taskService.GetNextPendingTaskAsync(cancellationToken);
        if (nextTask is not null)
        {
            // Process the task here
        }
    }
}
