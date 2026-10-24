using AgentUp.Desktop.Features.Validation.DTOs;

namespace AgentUp.Desktop.Features.Validation.Interfaces;

public interface IValidationArtifactStore
{
    string FlowDirectory(string outputRoot, string flowId);

    Task WriteScreenshotAsync(string path, Func<string, CancellationToken, Task> capture, CancellationToken cancellationToken);

    Task WriteReportAsync(string directory, ValidationRunReportDto report, CancellationToken cancellationToken);

    Task CopyLogAsync(string directory, string fileName, string? sourcePath, CancellationToken cancellationToken);
}
