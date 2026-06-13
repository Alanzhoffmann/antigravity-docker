using Microsoft.Extensions.Logging;
using PersonalBot.Chats.Interfaces;
using PersonalBot.Data.Interfaces;
using PersonalBot.Data.Models.Enums;

namespace PersonalBot.Chats;

public class ChatRunner
{
    private const int MaxRetries = 3;
    private readonly IChatResolver _chatResolver;
    private readonly ITaskService _taskService;
    private readonly ILogger<ChatRunner> _logger;

    public ChatRunner(IChatResolver chatResolver, ITaskService taskService, ILogger<ChatRunner> logger)
    {
        _chatResolver = chatResolver;
        _taskService = taskService;
        _logger = logger;
    }

    public async Task RunNextAsync(CancellationToken cancellationToken = default)
    {
        var currentChat = _chatResolver.ResolveCurrent();
        var nextTask = await _taskService.GetNextPendingAsync(cancellationToken);

        if (nextTask is null)
        {
            return;
        }

        if (nextTask.Agent != currentChat.AgentName)
        {
            // TODO figure out what to do here
            return;
        }

        try
        {
            nextTask.Agent = currentChat.AgentName;
            nextTask.Status = AiTaskStatus.Running;
            await _taskService.CommitAsync(cancellationToken);

            var result = await currentChat.GetResponseAsync(nextTask, cancellationToken: cancellationToken);
            nextTask.Status = AiTaskStatus.Completed;
        }
        catch (Exception ex)
        {
            nextTask.RetryCount++;

            if (nextTask.RetryCount >= MaxRetries)
            {
                _logger.LogError(
                    ex,
                    "Task for Issue #{IssueNum} failed after {MaxRetries} attempts. Error: {ExceptionMessage}",
                    nextTask.IssueNum,
                    MaxRetries,
                    ex.Message
                );
                nextTask.Status = AiTaskStatus.Failed;
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Inference failed for Issue #{IssueNum}. Re-queueing task. Remaining attempts: {RemainingAttempts}. Error: {ExceptionMessage}",
                    nextTask.IssueNum,
                    MaxRetries - nextTask.RetryCount,
                    ex.Message
                );
                nextTask.Status = AiTaskStatus.Pending; // Send back to queue
            }
        }
        finally
        {
            await _taskService.CommitAsync(cancellationToken);
        }
    }
}
