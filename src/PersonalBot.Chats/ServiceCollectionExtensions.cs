using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Chats.Options;
using PersonalBot.Tools;
using PersonalBot.Utils;

namespace PersonalBot.Chats;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChats(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(
            nameof(OllamaChat),
            (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptionsMonitor<OllamaOptions>>()
                    .CurrentValue;
                client.BaseAddress =
                    options.Url ?? throw new InvalidOperationException("Ollama url is missing");
                client.Timeout = Timeout.InfiniteTimeSpan;
            }
        );

        services.AddSingleton<IAgentChat, OllamaChat>();
        services.AddSingleton<IAgentChat, AgyChat>();
        services.AddSingleton<IAgentChat, NullChat>();

        services.AddTransient<IChatResolver, ChatResolver>();

        services.AddOptions<OllamaOptions>().BindConfiguration(OllamaOptions.SectionName);
        services.AddOptions<AgyOptions>().BindConfiguration(AgyOptions.SectionName);

        services.AddPersonalBotTools();
        services.AddUtils();

        return services;
    }
}
