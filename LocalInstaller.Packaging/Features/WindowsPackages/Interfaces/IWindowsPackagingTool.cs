using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.WindowsPackages.Models;

namespace LocalInstaller.Packaging.Features.WindowsPackages.Interfaces;

public interface IWindowsPackagingTool
{
    Task AcceptWixLicenseAsync(CancellationToken cancellationToken = default);
    Task BuildProductMsiAsync(WindowsPackageLayout layout, CancellationToken cancellationToken = default);
    Task BuildBundleAsync(PackageRequest request, WindowsPackageLayout layout, CancellationToken cancellationToken = default);
}
