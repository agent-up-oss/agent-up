using AgentUp.InstallerApp.Features.Capabilities.Models;
using AgentUp.InstallerApp.Features.Capabilities.Services;

namespace AgentUp.InstallerApp.Features.Capabilities.Controllers;

public sealed class CapabilitiesController(CapabilityDashboardService service)
{
    public bool SupportsInstallActions => service.SupportsInstallActions;

    public Task<IReadOnlyList<CapabilityCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default)
        => service.GetCatalogAsync(cancellationToken);

    public Task<IReadOnlyList<InstalledCapabilityModule>> GetInstalledAsync(CancellationToken cancellationToken = default)
        => service.GetInstalledAsync(cancellationToken);

    public Task<InstalledCapabilityModule> InstallAsync(CapabilityCatalogEntry entry, CancellationToken cancellationToken = default)
        => service.InstallAsync(entry, cancellationToken);

    public Task<InstalledCapabilityModule> SelectVersionAsync(
        InstalledCapabilityModule module,
        string version,
        CancellationToken cancellationToken = default)
        => service.SelectVersionAsync(module, version, cancellationToken);

}
