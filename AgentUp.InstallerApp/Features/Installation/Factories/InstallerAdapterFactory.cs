using AgentUp.InstallerConfig;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Features.Installation.Factories;

internal static class InstallerAdapterFactory
{
    public static IInstallerPlatformAdapter Create()
    {
        var product = AgentUpProductManifest();
        return InstallerPlatformAdapterFactory.Create(
            product,
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable),
            UseNixOsLookupOnlyMode());
    }

    public static bool UseNixOsLookupOnlyMode()
        => Environment.GetEnvironmentVariable(AgentUpProduct.NixOsLookupOnlyVariable) == "1"
           || InstallerPlatformAdapterFactory.IsNixOsHost();

    private static ProductManifest AgentUpProductManifest()
        => new(AgentUpProduct.Name, AgentUpProduct.Slug, AgentUpProduct.EnvironmentPrefix)
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli],
            WindowsUpgradeCode = AgentUpProduct.WindowsUpgradeCode
        };
}
