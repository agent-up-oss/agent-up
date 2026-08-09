using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.UbuntuPackages.Services;

namespace LocalInstaller.Packaging.Features.UbuntuPackages.Controllers;

public sealed class UbuntuPackageController : IUbuntuPackageController
{
    private readonly UbuntuPackager _packager;

    public UbuntuPackageController(UbuntuPackager packager)
    {
        _packager = packager;
    }

    public Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
        => _packager.PackageAsync(request, cancellationToken);
}
