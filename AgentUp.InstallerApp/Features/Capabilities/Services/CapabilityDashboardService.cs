using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Services;
using AgentUp.InstallerApp.Features.Capabilities.Interfaces;
using AgentUp.InstallerApp.Features.Capabilities.Models;
using AgentUp.InstallerApp.Features.Capabilities.Providers;

namespace AgentUp.InstallerApp.Features.Capabilities.Services;

public sealed class CapabilityDashboardService(
    ICapabilityCatalogProvider catalog,
    ICapabilityModuleStore store,
    CapabilityInstallPlanner installPlanner,
    bool supportsInstallActions = true)
{
    public bool SupportsInstallActions { get; } = supportsInstallActions;

    public Task<IReadOnlyList<CapabilityCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        catalog.GetCatalogAsync(cancellationToken);

    public Task<IReadOnlyList<InstalledCapabilityModule>> GetInstalledAsync(CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);

    public async Task<InstalledCapabilityModule> InstallAsync(CapabilityCatalogEntry entry, CancellationToken cancellationToken = default)
    {
        if (!SupportsInstallActions)
            throw new NotSupportedException("Capability modules are managed by NixOS or Home Manager configuration.");

        var artifact = entry.Versions.First();
        _ = installPlanner.Plan(artifact);
        var installed = (await store.LoadAsync(cancellationToken)).Where(module => module.Id != entry.Id).ToList();
        var module = new InstalledCapabilityModule(
            entry.Id,
            entry.DisplayName,
            entry.Description,
            artifact.Version,
            [new CapabilityInstalledVersion(entry.Id, artifact.Version, artifact.DownloadUrl.ToString(), CapabilityVersionSource.AgentUpManaged, true)]);

        installed.Add(module);
        await store.SaveAsync(installed.OrderBy(module => module.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(), cancellationToken);
        return module;
    }

    public async Task<InstalledCapabilityModule> SelectVersionAsync(
        InstalledCapabilityModule module,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsInstallActions)
            throw new NotSupportedException("Capability versions are managed by NixOS or Home Manager configuration.");

        var modules = (await store.LoadAsync(cancellationToken)).ToList();
        var updated = module with { ActiveVersion = version };
        modules.RemoveAll(item => item.Id == module.Id);
        modules.Add(updated);
        await store.SaveAsync(modules.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(), cancellationToken);
        return updated;
    }

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
        return new CapabilityDashboardService(
            new OfficialCapabilityCatalogProvider(),
            new NixOsCapabilityModuleStore(new CapabilityInventoryFileProvider()),
            new CapabilityInstallPlanner(new CapabilityToolCacheLayout(Path.Join(root, "tool-cache"))),
            false);
    }

    public static CapabilityDashboardService CreateFake()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-InstallerApp-Tests", Guid.NewGuid().ToString());
        return new CapabilityDashboardService(
            new OfficialCapabilityCatalogProvider(),
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
