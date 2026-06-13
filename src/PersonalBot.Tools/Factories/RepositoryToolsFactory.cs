using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PersonalBot.Tools.Factories;

public class RepositoryToolsFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RepositoryToolsFactory> _logger;

    public RepositoryToolsFactory(IServiceProvider serviceProvider, ILogger<RepositoryToolsFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ValueTask<RepositoryTools> CreateAsync(string repoPath)
    {
        _logger.LogInformation("Creating RepositoryTools for repo path: '{RepoPath}'", repoPath);

        var tools = ActivatorUtilities.CreateInstance<RepositoryTools>(_serviceProvider, repoPath);
        return ValueTask.FromResult(tools);
    }
}
