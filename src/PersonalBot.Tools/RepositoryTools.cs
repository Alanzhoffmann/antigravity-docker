using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PersonalBot.Utils;

namespace PersonalBot.Tools;

public class RepositoryTools
{
    private string _repoPath;
    private readonly ILogger<RepositoryTools> _logger;
    private readonly ProcessUtils _processUtils;

    public RepositoryTools(
        string repoPath,
        ILogger<RepositoryTools> logger,
        ProcessUtils processUtils
    )
    {
        _repoPath = repoPath;
        _logger = logger;
        _processUtils = processUtils;
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
            string gitArguments,
        CancellationToken cancellationToken = default
    )
    {
        return await _processUtils.RunProcessAsync(
            "git",
            [gitArguments],
            _repoPath,
            cancellationToken: cancellationToken
        );
    }

    [Description(
        "Executes a gh command in the repository workspace. Use this to interact with GitHub issues, PRs, etc."
    )]
    public async Task<string> RunGhCommand(
        [Description(
            "The gh arguments, e.g., 'issue list' or 'pr create --title \"New PR\" --body \"This is a new pull request.\"'"
        )]
            string ghArguments,
        CancellationToken cancellationToken = default
    )
    {
        return await _processUtils.RunProcessAsync(
            "gh",
            [ghArguments],
            _repoPath,
            cancellationToken: cancellationToken
        );
    }
}
