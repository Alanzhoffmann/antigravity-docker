using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

public class RoslynAgentTools:IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private Solution? _currentSolution;
    private readonly ILogger<RoslynAgentTools> _logger;
    private bool _disposed = false;

    public RoslynAgentTools(ILogger<RoslynAgentTools> logger)
    {
        // Must be called once per app domain before creating the workspace
        if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered)
        {
            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
        }
        
        _logger = logger;

        _workspace = MSBuildWorkspace.Create();
        _logger.LogInformation("MSBuildWorkspace created successfully.");
    }

    public Solution CurrentSolution => _currentSolution ?? throw new InvalidOperationException("No solution or project loaded. Please call LoadSolutionAsync or LoadProjectAsync first.");

    internal async Task LoadSolutionAsync(string solutionPath)
    {
        _currentSolution = await _workspace.OpenSolutionAsync(solutionPath);
    }

    internal async Task LoadProjectAsync(string projectPath)
    {
        var project = await _workspace.OpenProjectAsync(projectPath);
        _currentSolution = project.Solution;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // This unloads the solution and releases all memory/file locks
                _workspace?.Dispose(); 
                _logger.LogInformation("MSBuildWorkspace successfully disposed and solution unloaded.");
            }
            _disposed = true;
        }
    }
}