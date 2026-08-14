using LocalInstaller.App.Features.Capabilities.Models;

namespace LocalInstaller.App.Features.Capabilities.Interfaces;

public interface ICapabilityCatalogProvider
{
    Task<IReadOnlyList<CapabilityCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default);
}
