using AgentUp.AUDebug.Features.Validation.DTOs;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Features.Validation.Interfaces;

public interface IDesktopValidationLaunch
{
    string ResolveOutputPath(string? outputPath);

    Task<ProcessResult> RunAsync(DesktopValidationLaunchSpec spec, CancellationToken cancellationToken);
}
