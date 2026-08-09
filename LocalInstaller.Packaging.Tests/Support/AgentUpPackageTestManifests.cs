using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Tests.Support;

internal static class AgentUpPackageTestManifests
{
    private const string ProductName = "Agent-Up";
    private const string ProductSlug = "agent-up";
    private const string EnvironmentPrefix = "AGENTUP";
    private const string WindowsUpgradeCode = "8f9076cc-95f1-4bd6-a087-1686e5e0d540";

    public static PackageProductManifest Product()
        => new(ProductName, ProductSlug, EnvironmentPrefix)
        {
            Manufacturer = ProductName,
            WindowsUpgradeCode = WindowsUpgradeCode
        };

    public static ProductManifest InstallerProduct()
        => new(ProductName, ProductSlug, EnvironmentPrefix)
        {
            WindowsUpgradeCode = WindowsUpgradeCode
        };
}
