using AgentUp.InstallerConfig;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Tests.Support;

internal static class AgentUpPackageTestManifests
{
    public static PackageProductManifest Product()
        => new(AgentUpProduct.Name, AgentUpProduct.Slug, AgentUpProduct.EnvironmentPrefix)
        {
            Manufacturer = AgentUpProduct.Name,
            WindowsUpgradeCode = AgentUpProduct.WindowsUpgradeCode
        };

    public static ProductManifest InstallerProduct()
        => new(AgentUpProduct.Name, AgentUpProduct.Slug, AgentUpProduct.EnvironmentPrefix)
        {
            WindowsUpgradeCode = AgentUpProduct.WindowsUpgradeCode
        };
}
