using AgentUp.CLI.Features.Capabilities.DTOs;

namespace AgentUp.CLI.Features.Capabilities.Interfaces;

public interface ICapabilitiesArgParser
{
    (CapabilitiesCommand? Command, string? Error) Parse(string[] args);
}
