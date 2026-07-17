using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.MacOsPackages.Models;

namespace AgentUp.Packaging.Features.MacOsPackages.Interfaces;

public interface IMacOsPackageTool
{
    Task BuildComponentPackagesAsync(PackageRequest request, MacOsPackageLayout layout, CancellationToken cancellationToken = default);
    Task BuildProductPackageAsync(MacOsPackageLayout layout, CancellationToken cancellationToken = default);
}
