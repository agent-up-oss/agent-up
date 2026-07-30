using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Shared.Factories;

public sealed partial class PackagingServiceRegistry
{
    public PackagingServiceRegistry()
        : this(PackageProductManifest.AgentUp())
    {
    }
}
