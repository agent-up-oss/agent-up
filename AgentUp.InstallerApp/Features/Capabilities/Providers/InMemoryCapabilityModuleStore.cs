using AgentUp.InstallerApp.Features.Capabilities.Interfaces;
using AgentUp.InstallerApp.Features.Capabilities.Models;

namespace AgentUp.InstallerApp.Features.Capabilities.Providers;

public sealed class InMemoryCapabilityModuleStore : ICapabilityModuleStore
{
    private IReadOnlyList<InstalledCapabilityModule> _modules = [];

    public Task<IReadOnlyList<InstalledCapabilityModule>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_modules);

    public Task SaveAsync(IReadOnlyList<InstalledCapabilityModule> modules, CancellationToken cancellationToken = default)
    {
        _modules = modules.ToList();
        return Task.CompletedTask;
    }
}
