using AgentUp.CLI.Features.Capabilities.DTOs;

namespace AgentUp.CLI.Features.Capabilities.Interfaces;

public interface ICapabilityPacker
{
    CapabilityPackResult PackFirstParty(string workingDirectory);
}
