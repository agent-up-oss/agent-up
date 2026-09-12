using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Claude.Features.ClaudeCapability.Interfaces;

public interface IClaudeVersionProvider
{
    Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken);

    CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions);
}
