using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Internal;

namespace PersonalBot.Data.BackgroundServices;

public class MigrationBackgroundService<T> : BackgroundService
    where T : DbContext
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MigrationBackgroundService<T>> _logger;

    private readonly MigrationState<T> _migrationState;

    public MigrationBackgroundService(IServiceScopeFactory serviceScopeFactory, ILogger<MigrationBackgroundService<T>> logger, MigrationState<T> migrationState)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _migrationState = migrationState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogWaitingForMigrations();

            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<T>();
            await dbContext.Database.MigrateAsync(stoppingToken);
            _logger.LogMigrationsCompleted();
            _migrationState.IsDone = true;
        }
        catch (Exception ex)
        {
            _logger.LogMigrationError(ex);
        }
    }
}

internal static partial class MigrationLoggerExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Waiting for database migrations to complete...")]
    public static partial void LogWaitingForMigrations(this ILogger logger);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Database migrations completed successfully.")]
    public static partial void LogMigrationsCompleted(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Critical, Message = "Failed to apply database migrations.")]
    public static partial void LogMigrationError(this ILogger logger, Exception ex);
}
