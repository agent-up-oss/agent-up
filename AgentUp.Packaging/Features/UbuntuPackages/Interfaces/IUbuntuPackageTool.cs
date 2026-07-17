using AgentUp.Packaging.Features.UbuntuPackages.Models;

namespace AgentUp.Packaging.Features.UbuntuPackages.Interfaces;

public interface IUbuntuPackageTool
{
    Task BuildDebAsync(UbuntuPackageLayout layout, CancellationToken cancellationToken = default);
}
