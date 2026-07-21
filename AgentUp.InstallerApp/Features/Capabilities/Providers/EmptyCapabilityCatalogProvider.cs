using AgentUp.InstallerApp.Features.Capabilities.Interfaces;
using AgentUp.InstallerApp.Features.Capabilities.Models;

namespace AgentUp.InstallerApp.Features.Capabilities.Providers;

public sealed class EmptyCapabilityCatalogProvider : ICapabilityCatalogProvider
{
    public Task<IReadOnlyList<CapabilityCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CapabilityCatalogEntry>>([]);
}
