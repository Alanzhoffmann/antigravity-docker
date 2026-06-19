using Microsoft.Extensions.DependencyInjection;
using PersonalBot.Chats;
using PersonalBot.Data;
using PersonalBot.Workflows.BackgroundServices;
using PersonalBot.Workflows.Behaviors;

namespace PersonalBot.Workflows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflows(this IServiceCollection services)
    {
        services.AddMediator(options => options.PipelineBehaviors = [typeof(RepoPipelineBehavior<,>), typeof(RestoreAgentSessionPipelineBehavior<,>)]);
        services.AddHostedService<WorkflowRunnerBackgroundService>();
        services.AddDatabase();
        services.AddChats();

        return services;
    }
}
