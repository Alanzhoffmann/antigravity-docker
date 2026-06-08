using PersonalBot.Data.Models;

namespace PersonalBot.Data.Interfaces;

public interface ITaskQueryService
{
    Task<AiTask?> GetNextPendingTaskAsync(CancellationToken cancellationToken = default);
}
