using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.UbuntuPackages.Controllers;

public interface IUbuntuPackageController
{
    Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default);
}
