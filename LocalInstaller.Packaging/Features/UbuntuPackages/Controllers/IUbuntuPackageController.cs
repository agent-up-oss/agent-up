using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.UbuntuPackages.Controllers;

public interface IUbuntuPackageController
{
    Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default);
}
