using Microsoft.Extensions.Logging;

namespace PersonalBot.Tools.Factories;

public class RoslynAgentToolsFactory
{
    private readonly ILogger<RoslynAgentToolsFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public RoslynAgentToolsFactory(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<RoslynAgentToolsFactory>();
        _loggerFactory = loggerFactory;
    }

    public async Task<RoslynAgentTools> CreateAsync(string repoPath)
    {
        _logger.LogInformation(
            "Creating RoslynAgentTools instance for repository: {RepoPath}",
            repoPath
        );

        // find solution or project file in repo path
        var solutionFiles = Directory.GetFiles(repoPath, "*.sln", SearchOption.AllDirectories);
        var projectFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories);
        if (solutionFiles.Length == 0 && projectFiles.Length == 0)
        {
            _logger.LogWarning(
                "No solution or project file found in repository path: {RepoPath}",
                repoPath
            );
            throw new FileNotFoundException(
                "No solution (.sln) or project (.csproj) file found in repository path.",
                repoPath
            );
        }

        var solutionOrProjectFile = solutionFiles.FirstOrDefault() ?? projectFiles.First();
        _logger.LogInformation("Found solution/project file: {FilePath}", solutionOrProjectFile);

        var logger = _loggerFactory.CreateLogger<RoslynAgentTools>();
        var tools = new RoslynAgentTools(logger);

        if (solutionFiles.Length > 0)
        {
            _logger.LogInformation("Loading solution file: {SolutionFile}", solutionOrProjectFile);
            await tools.LoadSolutionAsync(solutionOrProjectFile);
        }
        else
        {
            _logger.LogInformation("Loading project file: {ProjectFile}", solutionOrProjectFile);
            await tools.LoadProjectAsync(solutionOrProjectFile);
        }

        return tools;
    }
}
