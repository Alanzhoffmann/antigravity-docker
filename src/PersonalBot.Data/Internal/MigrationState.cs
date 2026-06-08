using PersonalBot.Data.Interfaces;

namespace PersonalBot.Data.Internal;

public class MigrationState<T>
{
    public bool IsDone { get; set; }
}
