using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PersonalBot.Utils;

namespace PersonalBot.Tools;

public class RepositoryTools
{
    private readonly string _repoPath;
    private readonly ILogger<RepositoryTools> _logger;
    private readonly ProcessUtils _processUtils;

    public RepositoryTools(string repoPath, ILogger<RepositoryTools> logger, ProcessUtils processUtils)
    {
        _repoPath = repoPath;
        _logger = logger;
        _processUtils = processUtils;
    }

    public IList<AITool> ReadOnlyTools => [AIFunctionFactory.Create(RunBashCommand), AIFunctionFactory.Create(ReadFile)];

    public IList<AITool> Tools => [.. ReadOnlyTools, AIFunctionFactory.Create(WriteFile)];

    [Description("Reads the contents of a specific file in the repository.")]
    public async Task<string> ReadFile([Description("The relative path to the file, e.g., 'src/Program.cs'")] string relativeFilePath)
    {
        string fullPath = Path.Combine(_repoPath, relativeFilePath);

        _logger.LogInformation("Reading file {FilePath}", fullPath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File {FilePath} not found", fullPath);
            return $"Error: File {relativeFilePath} not found.";
        }

        return await File.ReadAllTextAsync(fullPath);
    }

    [Description("Overwrites a specific file in the repository with new code.")]
    public async Task<string> WriteFile(
        [Description("The relative path to the file to modify")] string relativeFilePath,
        [Description("The complete, updated file content")] string newContent
    )
    {
        string fullPath = Path.Combine(_repoPath, relativeFilePath);

        _logger.LogInformation("Writing to file {FilePath}", fullPath);

        await File.WriteAllTextAsync(fullPath, newContent);
        return $"Success: File {relativeFilePath} has been updated.";
    }

    [Description(
        "Executes a raw bash command in the repository workspace. Supports pipes (|), redirects (>), and standard Linux utilities like ls, grep, find, and git."
    )]
    public async Task<string> RunBashCommand(
        [Description("The full bash command to execute, e.g., 'ls -la' or 'git log --oneline | grep fix'")] string command,
        CancellationToken cancellationToken = default
    )
    {
        var response = await _processUtils.RunProcessAsync("/bin/bash", ["-c", command], _repoPath, cancellationToken: cancellationToken);

        return string.IsNullOrWhiteSpace(response) ? "Command executed successfully (no output)." : response;
    }
}
