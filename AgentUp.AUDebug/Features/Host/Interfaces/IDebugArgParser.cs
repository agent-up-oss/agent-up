using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Host.Interfaces;

public interface IDebugArgParser
{
    (DebugCommandDto? Command, string? Error) Parse(string[] args);
}
