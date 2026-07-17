using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.WindowsPackages.Controllers;

public interface IWindowsPackageController
{
    Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default);
}
