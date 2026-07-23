using AgentUp.Desktop.Features.FirstRun.DTOs;

namespace AgentUp.Desktop.Features.FirstRun.Interfaces;

public interface IFirstRunProcessProvider
{
    Task<FirstRunProcessResult> RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string? workingDirectory = null);
}
