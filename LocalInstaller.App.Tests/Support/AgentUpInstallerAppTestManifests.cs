using AgentUp.InstallerConfig;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Tests.Support;

internal static class AgentUpInstallerAppTestManifests
{
    public static ProductManifest Product()
        => new(AgentUpProduct.Name, AgentUpProduct.Slug, AgentUpProduct.EnvironmentPrefix)
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli],
            WindowsUpgradeCode = AgentUpProduct.WindowsUpgradeCode
        };

    public static PayloadSelection BundledPayload(Version version)
        => PayloadSelection.Bundled(AgentUpProduct.Name, version);
}
