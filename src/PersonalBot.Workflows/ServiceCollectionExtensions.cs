using Microsoft.Extensions.DependencyInjection;

namespace PersonalBot.Workflows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflows(this IServiceCollection services)
    {
        services.AddMediator();
        return services;
    }
}
