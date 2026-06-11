using Microsoft.Extensions.DependencyInjection;
using PersonalBot.Chats;
using PersonalBot.Data;

namespace PersonalBot.Workers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        services.AddDatabase();
        services.AddHostedService<QueueProcessor>();

        services.AddChats();

        return services;
    }
}
