using Microsoft.Extensions.Logging;

namespace PersonalBot.Tools.Factories;

public class RepositoryToolsFactory
{
    private readonly ILogger<RepositoryTools> _logger;

    public RepositoryToolsFactory(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<RepositoryTools>();
    }

    public ValueTask<RepositoryTools> CreateAsync(string repoPath)
    {
        _logger.LogInformation("Creating RepositoryTools for repo path: '{RepoPath}'", repoPath);

        var tools = new RepositoryTools(repoPath, _logger);
        return ValueTask.FromResult(tools);
    }
}