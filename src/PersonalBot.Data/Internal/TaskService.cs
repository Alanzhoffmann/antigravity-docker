using Microsoft.EntityFrameworkCore;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models;
using PersonalBot.Data.Models.Enums;

namespace PersonalBot.Data.Internal;

public class TaskService : ITaskService
{
    private readonly BotDbContext _dbContext;

    public TaskService(BotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<AiTask?> GetNextPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!_dbContext.MigrationState.IsDone)
        {
            return null;
        }

        return await _dbContext.AiTasks.OrderBy(t => t.CreatedAt).FirstOrDefaultAsync(t => t.Status == AiTaskStatus.Pending, cancellationToken);
    }

    public async void AddNew(AiTask aiTask) => _dbContext.Add(aiTask);

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        if (!_dbContext.MigrationState.IsDone)
        {
            return;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
