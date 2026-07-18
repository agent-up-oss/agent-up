using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Interfaces;

public interface IDotnetVersionProvider
{
    Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken);
}
