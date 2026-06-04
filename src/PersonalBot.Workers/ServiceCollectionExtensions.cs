using Microsoft.Extensions.DependencyInjection;

namespace PersonalBot.Workers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<QueueProcessor>();

        return services;
    }
}
