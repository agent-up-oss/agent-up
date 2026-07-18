using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;

namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;

public interface ICapabilityCommandRunner
{
    Task<CapabilityCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
