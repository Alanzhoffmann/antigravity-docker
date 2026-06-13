using PersonalBot.Data.Models;

namespace PersonalBot.Data.Interfaces;

public interface ITaskService
{
    ValueTask<AiTask?> GetNextPendingAsync(CancellationToken cancellationToken = default);
    void AddNew(AiTask aiTask);
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}
