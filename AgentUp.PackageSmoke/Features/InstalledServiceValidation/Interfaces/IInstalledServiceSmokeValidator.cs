using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;

public interface IInstalledServiceSmokeValidator : IDisposable
{
    Task<InstalledServiceSmokeResult> ValidateAsync(InstalledServiceSmokeRequest request, CancellationToken cancellationToken = default);
}
