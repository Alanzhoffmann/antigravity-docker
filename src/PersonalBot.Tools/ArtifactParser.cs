using Microsoft.Extensions.Logging;

namespace PersonalBot.Tools;

public class ArtifactParser
{
    private readonly ILogger<ArtifactParser> _logger;

    public ArtifactParser(ILogger<ArtifactParser> logger)
    {
        _logger = logger;
    }

    public async ValueTask<string> TryReadPlanArtifact(string directory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Artifact directory not found: '{Directory}'", directory);
            return string.Empty;
        }

        var candidates = Directory
            .GetFiles(directory, "implementation_plan.md", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
            .ToArray();

        if (candidates.Length == 0)
        {
            _logger.LogWarning("No markdown artifacts in '{Directory}'", directory);
            return string.Empty;
        }

        try
        {
            string content = await File.ReadAllTextAsync(candidates[0], cancellationToken);
            _logger.LogInformation("Read plan artifact '{PlanArtifact}': {ContentLength} chars", candidates[0], content.Length);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed reading artifact: {ExceptionMessage}", ex.Message);
            return string.Empty;
        }
    }
}
