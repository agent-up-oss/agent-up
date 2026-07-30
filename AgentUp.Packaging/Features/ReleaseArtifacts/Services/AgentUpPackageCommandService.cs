using AgentUp.Packaging.Features.MacOsPackages.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Controllers;
using AgentUp.Packaging.Features.WindowsPackages.Controllers;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Services;

public sealed partial class PackageCommandService
{
    public PackageCommandService(
        IPackageCommandParser parser,
        IRepositoryPathProvider repositoryPaths,
        IEnvironmentVariableProvider environment,
        IUbuntuPackageController ubuntu,
        IWindowsPackageController windows,
        IMacOsPackageController macOs)
        : this(parser, repositoryPaths, environment, ubuntu, windows, macOs, PackageProductManifest.AgentUp())
    {
    }
}
