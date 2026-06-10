using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace PersonalBot.Tools;

public class RepositoryTools
{
    private string _repoPath;
    private readonly ILogger<RepositoryTools> _logger;

    public RepositoryTools(string repoPath, ILogger<RepositoryTools> logger)
    {
        _repoPath = repoPath;
        _logger = logger;
    }

    public IList<AITool> Tools =>
        new List<AITool>
        {
            AIFunctionFactory.Create(RunGitCommand),
            AIFunctionFactory.Create(ReadFile),
            AIFunctionFactory.Create(WriteFile),
        };

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

    [Description(
        "Executes a gh command in the repository workspace. Use this to interact with GitHub issues, PRs, etc."
    )]
    public async Task<string> RunGhCommand(
        [Description(
            "The gh arguments, e.g., 'issue list' or 'pr create --title \"New PR\" --body \"This is a new pull request.\"'"
        )]
            string ghArguments
    )
    {
        _logger.LogInformation(
            "Running gh command: 'gh {GhArguments}' in '{RepoPath}'",
            ghArguments,
            _repoPath
        );

        var output = await Process.RunAndCaptureTextAsync(
            new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = ghArguments,
                WorkingDirectory = _repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }
        );

        _logger.LogInformation(
            "Gh command completed with exit code {ExitCode}",
            output.ExitStatus.ExitCode
        );

        return output.ExitStatus.ExitCode == 0
            ? output.StandardOutput
            : $"Gh Error: {output.StandardError}";
    }
}
