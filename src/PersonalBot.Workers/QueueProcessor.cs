using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalBot.Chats;

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
            var chatRunner = scope.ServiceProvider.GetRequiredService<ChatRunner>();
            
            await chatRunner.RunNextAsync(stoppingToken);
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
