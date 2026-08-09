using LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Interfaces;
using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.Services;

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
