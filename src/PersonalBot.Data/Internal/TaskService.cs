using Microsoft.EntityFrameworkCore;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models;
using PersonalBot.Data.Models.Enums;

namespace PersonalBot.Data.Internal;

public class TaskService : ITaskService
{
    private readonly BotDbContext _dbContext;
    private readonly MigrationState<BotDbContext> _migrationState;

    public TaskService(BotDbContext dbContext, MigrationState<BotDbContext> migrationState)
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

    public void AddNewTask(AiTask aiTask) => _dbContext.Add(aiTask);

    public async Task CommitAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
