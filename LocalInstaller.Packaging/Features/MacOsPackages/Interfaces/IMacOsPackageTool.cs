using LocalInstaller.Packaging.Features.MacOsPackages.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.MacOsPackages.Interfaces;

public interface IMacOsPackageTool
{
    Task BuildComponentPackagesAsync(PackageRequest request, MacOsPackageLayout layout, MacOsPackageManifest manifest, CancellationToken cancellationToken = default);
    Task BuildProductPackageAsync(MacOsPackageLayout layout, CancellationToken cancellationToken = default);
}
