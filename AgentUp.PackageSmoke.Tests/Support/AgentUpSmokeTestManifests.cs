using AgentUp.InstallerConfig;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

namespace AgentUp.PackageSmoke.Tests.Support;

internal static class AgentUpSmokeTestManifests
{
    public static SmokeProductConfig Product()
        => new(
            ServiceName: AgentUpProduct.Slug + "-server",
            CliShimName: AgentUpProduct.Slug,
            ArtifactBaseName: AgentUpProduct.Slug,
            DisplayName: AgentUpProduct.Name,
            InstallDirName: AgentUpProduct.Name);
}
