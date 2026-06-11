using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PersonalBot.Tools.Factories;

public class RoslynAgentToolsFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RoslynAgentToolsFactory> _logger;

    public RoslynAgentToolsFactory(
        IServiceProvider serviceProvider,
        ILogger<RoslynAgentToolsFactory> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<RoslynAgentTools> CreateAsync(string repoPath)
    {
        _logger.LogInformation(
            "Creating RoslynAgentTools instance for repository: {RepoPath}",
            repoPath
        );

        // find solution or project file in repo path
        var solutionFiles = Directory.GetFiles(repoPath, "*.slnx", SearchOption.AllDirectories);
        var projectFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories);
        if (solutionFiles.Length == 0 && projectFiles.Length == 0)
        {
            _logger.LogWarning(
                "No solution or project file found in repository path: {RepoPath}",
                repoPath
            );
            throw new FileNotFoundException(
                "No solution (.slnx) or project (.csproj) file found in repository path.",
                repoPath
            );
        }

        var solutionOrProjectFile = solutionFiles.FirstOrDefault() ?? projectFiles.First();
        _logger.LogInformation("Found solution/project file: {FilePath}", solutionOrProjectFile);

        var tools = ActivatorUtilities.CreateInstance<RoslynAgentTools>(_serviceProvider);

        if (solutionFiles.Length > 0)
        {
            await tools.LoadSolutionAsync(solutionOrProjectFile);
        }
        else
        {
            await tools.LoadProjectAsync(solutionOrProjectFile);
        }

        return tools;
    }
}
