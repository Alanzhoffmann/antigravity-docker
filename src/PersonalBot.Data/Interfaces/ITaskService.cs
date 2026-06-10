using PersonalBot.Data.Models;

namespace PersonalBot.Data.Interfaces;

public interface ITaskService
{
    Task<AiTask?> GetNextPendingTaskAsync(CancellationToken cancellationToken = default);
    void AddNewTask(AiTask aiTask);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
