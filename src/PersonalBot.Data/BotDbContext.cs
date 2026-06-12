using Microsoft.EntityFrameworkCore;
using PersonalBot.Data.Internal;
using PersonalBot.Data.Models;

namespace PersonalBot.Data;

public class BotDbContext : DbContext
{
    public BotDbContext(
        DbContextOptions<BotDbContext> dbOptions,
        MigrationState<BotDbContext> migrationState
    )
        : base(dbOptions)
    {
        MigrationState = migrationState;
    }

    public DbSet<AiTask> AiTasks => Set<AiTask>();
    public DbSet<GitHubWebhook> Webhooks => Set<GitHubWebhook>();

    public MigrationState<BotDbContext> MigrationState { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}
