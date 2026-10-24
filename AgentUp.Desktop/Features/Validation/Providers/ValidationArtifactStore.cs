using System.Text.Json;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;

namespace AgentUp.Desktop.Features.Validation.Providers;

public sealed class ValidationArtifactStore : IValidationArtifactStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string FlowDirectory(string outputRoot, string flowId)
    {
        var directory = Path.Join(Path.GetFullPath(outputRoot), SafeSegment(flowId));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public async Task WriteScreenshotAsync(
        string path,
        Func<string, CancellationToken, Task> capture,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        await capture(path, cancellationToken);
    }

    public async Task WriteReportAsync(
        string directory,
        ValidationRunReportDto report,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Join(directory, "report.json");
        var json = JsonSerializer.Serialize(report, Json);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public async Task CopyLogAsync(
        string directory,
        string fileName,
        string? sourcePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var destination = Path.Join(directory, fileName);
        await using var source = File.OpenRead(sourcePath);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static string SafeSegment(string flowId)
    {
        var chars = flowId.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' || ch == '.' ? ch : '_').ToArray();
        var value = new string(chars).Trim('.');
        return string.IsNullOrWhiteSpace(value) || value is "." or ".." ? "flow" : value;
    }
}
