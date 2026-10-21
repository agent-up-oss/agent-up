using AgentUp.CLI.Features.Capabilities.DTOs;

namespace AgentUp.CLI.Features.Capabilities.Interfaces;

public interface ICapabilityPackProcessRunner
{
    CapabilityProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory);
}
