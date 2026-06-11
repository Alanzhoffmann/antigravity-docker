using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PersonalBot.Utils;

public class ProcessUtils
{
    private readonly ILogger<ProcessUtils> _logger;

    public ProcessUtils(ILogger<ProcessUtils> logger)
    {
        _logger = logger;
    }

    public async Task<string> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Executing: {FileName} {Arguments} (cwd='{WorkingDirectory}')",
            fileName,
            arguments,
            workingDirectory
        );

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var sw = Stopwatch.StartNew();
        var output = await Process.RunAndCaptureTextAsync(
            startInfo,
            cancellationToken: cancellationToken
        );

        sw.Stop();

        _logger.LogInformation(
            "{Command} exited code={ExitCode} in {ElapsedSeconds:F1}s",
            fileName,
            output.ExitStatus.ExitCode,
            sw.Elapsed.TotalSeconds
        );

        if (!string.IsNullOrEmpty(output.StandardError))
            _logger.LogWarning("stderr: {StandardError}", output.StandardError.Trim());

        return output.ExitStatus.ExitCode == 0
            ? output.StandardOutput
            : $"{fileName} error: {output.StandardError}";
    }
}
