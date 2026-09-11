using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Interfaces;

public interface ICursorVersionProvider
{
    Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken);

    CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions);
}
