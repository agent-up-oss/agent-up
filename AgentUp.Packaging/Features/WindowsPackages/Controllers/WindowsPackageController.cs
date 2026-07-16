using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.WindowsPackages.Services;

namespace AgentUp.Packaging.Features.WindowsPackages.Controllers;

public sealed class WindowsPackageController : IWindowsPackageController
{
    private readonly WindowsPackager _packager;

    public WindowsPackageController(WindowsPackager packager)
    {
        _packager = packager;
    }

    public Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
        => _packager.PackageAsync(request, cancellationToken);
}
