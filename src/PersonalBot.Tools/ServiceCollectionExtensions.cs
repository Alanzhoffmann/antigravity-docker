using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalBot.Tools.Factories;

namespace PersonalBot.Tools;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalBotTools(this IServiceCollection services)
    {
        services.TryAddSingleton<RepositoryToolsFactory>();
        services.TryAddSingleton<RoslynAgentToolsFactory>();
        services.TryAddSingleton<ArtifactParser>();

        return services;
    }
}
