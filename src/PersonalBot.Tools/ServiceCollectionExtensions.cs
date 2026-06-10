using Microsoft.Extensions.DependencyInjection;
using PersonalBot.Tools.Factories;

namespace PersonalBot.Tools;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalBotTools(this IServiceCollection services)
    {
        services.AddSingleton<RepositoryToolsFactory>();
        services.AddSingleton<RoslynAgentToolsFactory>();
        services.AddSingleton<ArtifactParser>();

        return services;
    }
}