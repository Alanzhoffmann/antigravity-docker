using Microsoft.EntityFrameworkCore;
using PersonalBot.Data.Internal;
using PersonalBot.Data.Models;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> dbOptions, MigrationState<BotDbContext> migrationState)
        : base(dbOptions)
    {
        MigrationState = migrationState;
    }

    public DbSet<AiTask> AiTasks => Set<AiTask>();
    public DbSet<GitHubWebhook> Webhooks => Set<GitHubWebhook>();

    public DbSet<Workflow> Workflows => Set<Workflow>();

    public MigrationState<BotDbContext> MigrationState { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}
