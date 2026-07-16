using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages.Services;

namespace AgentUp.Packaging.Features.UbuntuPackages.Controllers;

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
