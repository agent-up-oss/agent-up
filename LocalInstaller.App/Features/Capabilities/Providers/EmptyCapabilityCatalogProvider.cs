using LocalInstaller.App.Features.Capabilities.Interfaces;
using LocalInstaller.App.Features.Capabilities.Models;

namespace LocalInstaller.App.Features.Capabilities.Providers;

public sealed class EmptyCapabilityCatalogProvider : ICapabilityCatalogProvider
{
    public Task<IReadOnlyList<CapabilityCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CapabilityCatalogEntry>>([]);
}
