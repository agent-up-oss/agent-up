using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.MacOsPackages.Controllers;

public interface IMacOsPackageController
{
    Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default);
}
