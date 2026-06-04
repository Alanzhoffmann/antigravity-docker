using Microsoft.Extensions.DependencyInjection;

namespace PersonalBot.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddDbContext<BotDbContext>();

        return services;
    }
}
