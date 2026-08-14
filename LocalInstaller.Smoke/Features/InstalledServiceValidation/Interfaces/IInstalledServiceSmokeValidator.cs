using LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.Interfaces;

public interface IInstalledServiceSmokeValidator : IDisposable
{
    Task<InstalledServiceSmokeResult> ValidateAsync(InstalledServiceSmokeRequest request, CancellationToken cancellationToken = default);
}
