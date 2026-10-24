using AgentUp.CLI.Features.Capabilities.DTOs;

namespace AgentUp.CLI.Features.Capabilities.Interfaces;

public interface ICapabilityPacker
{
    Task<CapabilityPackResult> PackFirstPartyAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
