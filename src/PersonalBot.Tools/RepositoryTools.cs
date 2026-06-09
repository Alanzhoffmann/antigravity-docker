using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace PersonalBot.Tools;

public class RepositoryTools
{
    private string _repoPath;
    private readonly ILogger<RepositoryTools> _logger;

    public RepositoryTools(ILogger<RepositoryTools> logger)
    {
        _repoPath = string.Empty;
        _logger = logger;
    }

    public IList<AITool> Tools =>
        new List<AITool>
        {
            AIFunctionFactory.Create(RunGitCommand),
            AIFunctionFactory.Create(ReadFile),
            AIFunctionFactory.Create(WriteFile),
        };

    public void SetRepoPath(string repoPath)
    {
        _logger.LogInformation(
            "Updating RepositoryTools repo path from '{OldPath}' to '{NewPath}'",
            _repoPath,
            repoPath
        );
        _repoPath = repoPath;
    }

    [Description("Reads the contents of a specific file in the repository.")]
    public async Task<string> ReadFile(
        [Description("The relative path to the file, e.g., 'src/Program.cs'")]
            string relativeFilePath
    )
    {
        string fullPath = Path.Combine(_repoPath, relativeFilePath);
        if (!File.Exists(fullPath))
            return $"Error: File {relativeFilePath} not found.";
        return await File.ReadAllTextAsync(fullPath);
    }

    [Description("Overwrites a specific file in the repository with new code.")]
    public async Task<string> WriteFile(
        [Description("The relative path to the file to modify")] string relativeFilePath,
        [Description("The complete, updated file content")] string newContent
    )
    {
        string fullPath = Path.Combine(_repoPath, relativeFilePath);
        await File.WriteAllTextAsync(fullPath, newContent);
        return $"Success: File {relativeFilePath} has been updated.";
    }

    [Description(
        "Executes a Git command in the repository workspace. Use this to create branches, commit, and push."
    )]
    public async Task<string> RunGitCommand(
        [Description(
            "The git arguments, e.g., 'checkout -b fix-issue-1' or 'commit -am \"Fix bug\"'"
        )]
            string gitArguments
    )
    {
        _logger.LogInformation(
            "Running git command: 'git {GitArguments}' in '{RepoPath}'",
            gitArguments,
            _repoPath
        );

        var output = await Process.RunAndCaptureTextAsync(
            new ProcessStartInfo
            {
                FileName = "git",
                Arguments = gitArguments,
                WorkingDirectory = _repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }
        );

        _logger.LogInformation(
            "Git command completed with exit code {ExitCode}",
            output.ExitStatus.ExitCode
        );

        return output.ExitStatus.ExitCode == 0
            ? output.StandardOutput
            : $"Git Error: {output.StandardError}";
    }
}
