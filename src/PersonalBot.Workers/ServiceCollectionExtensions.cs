using Microsoft.Extensions.DependencyInjection;
using PersonalBot.Chats;
using PersonalBot.Data;
using PersonalBot.Workflows;

namespace PersonalBot.Workers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        services.AddDatabase();
        services.AddHostedService<WebhookBackgroundService>();
        services.AddScoped<WebhookProcessor>();
        services.AddWorkflows();

        // services.AddHostedService<AiTaskQueueProcessor>();

        services.AddChats();

        return services;
    }
}
