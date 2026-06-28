using Microsoft.EntityFrameworkCore;
using PersonalBot.Data.Internal;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.ValueObjects;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> dbOptions, MigrationState<BotDbContext> migrationState)
        : base(dbOptions)
    {
        MigrationState = migrationState;
    }

    public DbSet<Workflow> Workflows => Set<Workflow>();

    public MigrationState<BotDbContext> MigrationState { get; }

    public async Task<Session?> GetSessionFromIssueAsync(string issueNumber, Guid currentWorkflowId, CancellationToken cancellationToken = default) =>
        await Set<ChatStarted>()
            .OrderByDescending(w => w.CreatedAt)
            .Where(w => w.IssueNumber == issueNumber && w.Status == WorkflowStatus.Completed && w.Id != currentWorkflowId)
            .Select(w => w.Session)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}
