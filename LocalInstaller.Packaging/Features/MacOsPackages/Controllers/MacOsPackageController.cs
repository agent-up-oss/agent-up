using LocalInstaller.Packaging.Features.MacOsPackages.Services;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.MacOsPackages.Controllers;

public sealed class MacOsPackageController : IMacOsPackageController
{
    private readonly MacOsPackager _packager;

    public MacOsPackageController(MacOsPackager packager)
    {
        _packager = packager;
    }

    public Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
        => _packager.PackageAsync(request, cancellationToken);
}
