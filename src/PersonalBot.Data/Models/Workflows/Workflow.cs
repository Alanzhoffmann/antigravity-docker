using Mediator;
using PersonalBot.Data.Models.Enums;

namespace PersonalBot.Data.Models.Workflows;

public abstract class Workflow : INotification
{
    public Guid Id { get; set; }
    public int RetryCount { get; set; }
    public WorkflowStatus Status { get; set; }
    public string? Output { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Workflow? ParentWorkflow { get; set; }
    public List<Workflow> ChildWorkflows { get; set; } = [];
}
