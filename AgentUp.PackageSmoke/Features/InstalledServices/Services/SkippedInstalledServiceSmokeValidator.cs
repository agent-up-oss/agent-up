using AgentUp.PackageSmoke.Features.InstalledServices.DTOs;
using AgentUp.PackageSmoke.Features.Validation.DTOs;

namespace AgentUp.PackageSmoke.Features.InstalledServices.Services;

public sealed class SkippedInstalledServiceSmokeValidator : IInstalledServiceSmokeValidator
{
    private readonly string _message;

    public SkippedInstalledServiceSmokeValidator(string message)
    {
        _message = message;
    }

    public Task<InstalledServiceSmokeResult> ValidateAsync(InstalledServiceSmokeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new InstalledServiceSmokeResult(null, [new SmokeFinding(FindingSeverity.Info, "installed.skipped", _message)]));
}
