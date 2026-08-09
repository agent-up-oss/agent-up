using LocalInstaller.Packaging.Features.UbuntuPackages.Models;

namespace LocalInstaller.Packaging.Features.UbuntuPackages.Interfaces;

public interface IUbuntuPackageTool
{
    Task BuildDebAsync(UbuntuPackageLayout layout, CancellationToken cancellationToken = default);
}
