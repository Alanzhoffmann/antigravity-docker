using Mediator;

namespace PersonalBot.Data.Models.Workflows;

public abstract class Workflow : INotification
{
    public Guid Id { get; set; }
}
