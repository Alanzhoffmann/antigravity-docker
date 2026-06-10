using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalBot.Data.BackgroundServices;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Internal;

namespace PersonalBot.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddDbContext<BotDbContext>();
        services.AddDataBackgroundServices<BotDbContext>();
        services.AddScoped<ITaskService, TaskService>();

        return services;
    }

    public static IServiceCollection AddDataBackgroundServices<T>(this IServiceCollection services)
        where T : DbContext
    {
        services.AddHostedService<MigrationBackgroundService<T>>();
        services.AddSingleton<MigrationState<T>>();

        return services;
    }
}
