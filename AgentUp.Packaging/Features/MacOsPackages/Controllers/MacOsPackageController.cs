using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.MacOsPackages.Services;

namespace AgentUp.Packaging.Features.MacOsPackages.Controllers;

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
