using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Services;

public sealed class SmokeCommandService
{
    private readonly ISmokeValidationProvider _validation;

    public SmokeCommandService(ISmokeValidationProvider validation)
    {
        _validation = validation;
    }

    public Task<SmokeCommandResult> RunAsync(SmokeCommandRequest request, CancellationToken cancellationToken = default)
        => request.Command switch
        {
            "validate-package" => _validation.ValidatePackageAsync(request, cancellationToken),
            "validate-installer-flow" => _validation.ValidateInstallerFlowAsync(request, cancellationToken),
            _ => _validation.ValidateInstalledServiceAsync(request, cancellationToken)
        };
}
