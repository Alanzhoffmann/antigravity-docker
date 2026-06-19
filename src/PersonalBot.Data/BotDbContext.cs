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

    public DbSet<Workflow> Workflows => Set<Workflow>();

    public MigrationState<BotDbContext> MigrationState { get; }

    public async Task<string?> GetSessionFromIssueAsync(string issueNumber, CancellationToken cancellationToken = default) =>
        await Set<ChatStarted>()
            .OrderByDescending(w => w.CreatedAt)
            .Where(w => w.IssueNumber == issueNumber)
            .Select(w => w.Session)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}
