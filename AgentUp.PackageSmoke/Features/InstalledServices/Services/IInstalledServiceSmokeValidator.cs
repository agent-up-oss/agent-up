using AgentUp.PackageSmoke.Features.InstalledServices.DTOs;

namespace AgentUp.PackageSmoke.Features.InstalledServices.Services;

public interface IInstalledServiceSmokeValidator
{
    Task<InstalledServiceSmokeResult> ValidateAsync(InstalledServiceSmokeRequest request, CancellationToken cancellationToken = default);
}
