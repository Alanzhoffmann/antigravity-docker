using Microsoft.EntityFrameworkCore;
using PersonalBot.Data.Models;

namespace PersonalBot.Data;

public class BotDbContext : DbContext
{
    public DbSet<AiTask> AiTasks => Set<AiTask>();
}
