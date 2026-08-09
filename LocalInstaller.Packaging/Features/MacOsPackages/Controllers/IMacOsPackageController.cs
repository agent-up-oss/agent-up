using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.MacOsPackages.Controllers;

public interface IMacOsPackageController
{
    Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default);
}
