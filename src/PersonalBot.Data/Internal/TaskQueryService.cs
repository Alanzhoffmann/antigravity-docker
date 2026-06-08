using Microsoft.EntityFrameworkCore;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models;

namespace PersonalBot.Data.Internal;

public class TaskQueryService : ITaskQueryService
{
    private readonly BotDbContext _dbContext;
    private readonly MigrationState<BotDbContext> _migrationState;

    public TaskQueryService(BotDbContext dbContext, MigrationState<BotDbContext> migrationState)
    {
        _dbContext = dbContext;
        _migrationState = migrationState;
    }

    public async Task<AiTask?> GetNextPendingTaskAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_migrationState.IsDone)
        {
            return null;
        }

        return await _dbContext
            .AiTasks.Where(t => t.Status == AiTaskStatus.Pending)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
