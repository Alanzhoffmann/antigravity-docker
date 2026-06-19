using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersonalBot.Data.Internal;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Data;

public class BotDbContext : DbContext
{
    private readonly ILogger<BotDbContext> _logger;

    public BotDbContext(DbContextOptions<BotDbContext> dbOptions, MigrationState<BotDbContext> migrationState, ILogger<BotDbContext> logger)
        : base(dbOptions)
    {
        MigrationState = migrationState;
        _logger = logger;
    }

    public DbSet<Workflow> Workflows => Set<Workflow>();

    public MigrationState<BotDbContext> MigrationState { get; }

    public async Task<string?> GetSessionFromIssueAsync(string issueNumber, CancellationToken cancellationToken = default)
    {
        var session = await Set<ChatStarted>()
            .OrderByDescending(w => w.CreatedAt)
            .Where(w => w.IssueNumber == issueNumber)
            .Select(w => w.Session)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        _logger.LogInformation("returning session for issue {IssueNumber}: {Session}", issueNumber, session);

        return session;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
    }
}
