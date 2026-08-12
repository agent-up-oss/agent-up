using LocalInstaller.App.Features.Capabilities.Models;

namespace LocalInstaller.App.Features.Capabilities.Interfaces;

public interface ICapabilityModuleStore
{
    Task<IReadOnlyList<InstalledCapabilityModule>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<InstalledCapabilityModule> modules, CancellationToken cancellationToken = default);
}
