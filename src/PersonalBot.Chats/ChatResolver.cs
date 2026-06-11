using Microsoft.Extensions.Logging;
using PersonalBot.Chats.Interfaces;

namespace PersonalBot.Chats;

public class ChatResolver : IChatResolver
{
    private readonly IEnumerable<IAgentChat> _agents;
    private readonly ILogger<ChatResolver> _logger;

    public ChatResolver(IEnumerable<IAgentChat> agents, ILogger<ChatResolver> logger)
    {
        _agents = [.. agents.OrderBy(x => x.SortOrder)];
        _logger = logger;
    }

    public IAgentChat ResolveCurrent()
    {
        var agent =
            _agents.FirstOrDefault(x => x.IsEnabled)
            ?? throw new InvalidOperationException("No enabled IAgentChat implementations found");
        _logger.LogInformation($"Using IAgentChat implementation: {agent.GetType().Name}");

        return agent;
    }
}
