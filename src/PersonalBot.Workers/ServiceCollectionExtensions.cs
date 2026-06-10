using Microsoft.Extensions.DependencyInjection;
using PersonalBot.Data;

namespace PersonalBot.Workers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        services.AddDatabase();
        services.AddHostedService<QueueProcessor>();

        return services;
    }
}
