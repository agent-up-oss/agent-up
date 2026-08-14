using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.WindowsPackages.Controllers;

public interface IWindowsPackageController
{
    Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default);
}
