using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;

namespace AgentUp.Packaging.Features.UbuntuPackages.Services;

public sealed partial class UbuntuPackager
{
    public UbuntuPackager(IPackageWriter writer, PayloadStagingController payloads, IUbuntuPackageTool packageTool)
        : this(writer, payloads, packageTool, PackageProductManifest.AgentUp())
    {
    }
}
