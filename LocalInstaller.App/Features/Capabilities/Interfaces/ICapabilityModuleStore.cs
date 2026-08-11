using AgentUp.InstallerApp.Features.Capabilities.Models;

namespace AgentUp.InstallerApp.Features.Capabilities.Interfaces;

public interface ICapabilityModuleStore
{
    Task<IReadOnlyList<InstalledCapabilityModule>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<InstalledCapabilityModule> modules, CancellationToken cancellationToken = default);
}
