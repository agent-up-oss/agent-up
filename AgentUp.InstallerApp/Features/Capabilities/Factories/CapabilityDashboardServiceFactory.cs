using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Services;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.InstallerApp.Features.Capabilities.Providers;
using AgentUp.InstallerApp.Features.Capabilities.Services;

namespace AgentUp.InstallerApp.Features.Capabilities.Factories;

public static class CapabilityDashboardServiceFactory
{
    public static CapabilityDashboardService CreateDefault()
    {
        var root = DefaultStateRoot();
        return new CapabilityDashboardService(
            new OfficialCapabilityCatalogProvider(),
            new FileCapabilityModuleStore(Path.Join(root, "capabilities.json")),
            new CapabilityInstallPlanner(new CapabilityToolCacheLayout(Path.Join(root, "tool-cache"))));
    }

    public static CapabilityDashboardService CreateNixOs()
    {
        var root = DefaultStateRoot();
        return CreateNixOs(Path.Join(root, "tool-cache"));
    }

    public static CapabilityDashboardService CreateNixOs(string cacheRoot)
        => new(
            new OfficialCapabilityCatalogProvider(),
            new NixOsCapabilityModuleStore(new CapabilityInventoryFileProvider()),
            new CapabilityInstallPlanner(new CapabilityToolCacheLayout(cacheRoot)),
            false);

    public static CapabilityDashboardService CreateFake()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerApp-Tests", Guid.NewGuid().ToString());
        return new CapabilityDashboardService(
            new OfficialCapabilityCatalogProvider(),
            new InMemoryCapabilityModuleStore(),
            new CapabilityInstallPlanner(new CapabilityToolCacheLayout(root)));
    }

    public static CapabilityDashboardService CreateEmpty()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerApp-Empty", Guid.NewGuid().ToString());
        return new CapabilityDashboardService(
            new EmptyCapabilityCatalogProvider(),
            new InMemoryCapabilityModuleStore(),
            new CapabilityInstallPlanner(new CapabilityToolCacheLayout(root)));
    }

    private static string DefaultStateRoot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(local)
            ? Path.Join(Path.GetTempPath(), "AgentUp")
            : Path.Join(local, "AgentUp", "Installer");
    }
}
