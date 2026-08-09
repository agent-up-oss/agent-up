using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

public interface ISmokeValidationProvider
{
    Task<SmokeCommandResult> ValidatePackageAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default);
    Task<SmokeCommandResult> ValidateInstallerFlowAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default);
    Task<SmokeCommandResult> ValidateInstalledServiceAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default);
}
