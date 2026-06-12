using Microsoft.Extensions.DependencyInjection;

namespace PersonalBot.Utils;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUtils(this IServiceCollection services)
    {
        services.AddTransient<ProcessUtils>();
        services.AddTransient<GitHubUtils>();
        return services;
    }
}
