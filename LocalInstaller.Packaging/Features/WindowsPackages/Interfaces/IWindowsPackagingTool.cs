using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.WindowsPackages.Models;

namespace AgentUp.Packaging.Features.WindowsPackages.Interfaces;

public interface IWindowsPackagingTool
{
    Task AcceptWixLicenseAsync(CancellationToken cancellationToken = default);
    Task BuildProductMsiAsync(WindowsPackageLayout layout, CancellationToken cancellationToken = default);
    Task BuildBundleAsync(PackageRequest request, WindowsPackageLayout layout, CancellationToken cancellationToken = default);
}
