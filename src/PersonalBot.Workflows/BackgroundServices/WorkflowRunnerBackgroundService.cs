using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalBot.Data;
using PersonalBot.Data.Models.Enums;
using PersonalBot.Data.Models.Workflows;

namespace PersonalBot.Workflows.BackgroundServices;

public class WorkflowRunnerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IPublisher _publisher;
    private readonly ILogger<WorkflowRunnerBackgroundService> _logger;

    public WorkflowRunnerBackgroundService(IServiceScopeFactory serviceScopeFactory, IPublisher publisher, ILogger<WorkflowRunnerBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BotDbContext>();
            Workflow? nextWorkflow;
            while (
                (
                    nextWorkflow = await context
                        .Workflows.Include(w => w.ParentWorkflow)
                        .OrderBy(w => w.CreatedAt)
                        .FirstOrDefaultAsync(
                            w => w.Status == WorkflowStatus.Pending && (w.ParentWorkflow == null || w.ParentWorkflow.Status == WorkflowStatus.Completed),
                            cancellationToken: stoppingToken
                        )
                )
                    is not null
            )
            {
                try
                {
                    _logger.LogInformation("Running workflow {Workflow}", nextWorkflow);
                    nextWorkflow.Status = WorkflowStatus.Running;
                    await context.SaveChangesAsync(stoppingToken);
                    await _publisher.Publish(nextWorkflow, stoppingToken);
                    nextWorkflow.Status = WorkflowStatus.Completed;
                }
                catch (Exception ex)
                {
                    if (nextWorkflow.RetryCount >= 3)
                    {
                        _logger.LogError(ex, "Failed to run workflow {Workflow}, marking as failed", nextWorkflow);
                        nextWorkflow.Status = WorkflowStatus.Failed;
                    }
                    else
                    {
                        _logger.LogError(ex, "Failed to run workflow {Workflow}, trying again later {RetryCount}", nextWorkflow, nextWorkflow.RetryCount);
                        nextWorkflow.RetryCount++;
                    }
                }
                finally
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
