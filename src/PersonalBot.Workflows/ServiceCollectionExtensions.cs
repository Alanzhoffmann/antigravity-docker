using Microsoft.Extensions.DependencyInjection;
using PersonalBot.Chats;
using PersonalBot.Data;
using PersonalBot.Workflows.BackgroundServices;

namespace PersonalBot.Workflows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflows(this IServiceCollection services)
    {
        services.AddMediator();
        services.AddHostedService<WorkflowRunnerBackgroundService>();
        services.AddDatabase();
        services.AddChats();

        return services;
    }
}
