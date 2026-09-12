using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Interfaces;

public interface ICodexVersionProvider
{
    Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken);

    CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions);
}
