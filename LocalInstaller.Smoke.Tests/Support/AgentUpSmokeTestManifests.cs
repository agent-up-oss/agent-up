using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

namespace AgentUp.PackageSmoke.Tests.Support;

internal static class AgentUpSmokeTestManifests
{
    private const string ProductName = "Agent-Up";
    private const string ProductSlug = "agent-up";

    public static SmokeProductConfig Product()
        => new(
            ServiceName: ProductSlug + "-server",
            CliShimName: ProductSlug,
            ArtifactBaseName: ProductSlug,
            DisplayName: ProductName,
            InstallDirName: ProductName);
}
