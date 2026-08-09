using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;

public sealed class SkippedInstalledServiceSmokeValidator : IInstalledServiceSmokeValidator
{
    private readonly string _message;

    public SkippedInstalledServiceSmokeValidator(string message)
    {
        _message = message;
    }

    public Task<InstalledServiceSmokeResult> ValidateAsync(InstalledServiceSmokeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new InstalledServiceSmokeResult(null, [new SmokeFinding(FindingSeverity.Info, "installed.skipped", _message)]));

    public void Dispose()
    {
    }
}
