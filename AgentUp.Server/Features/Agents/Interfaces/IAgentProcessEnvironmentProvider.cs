using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAgentProcessEnvironmentProvider
{
    IReadOnlyDictionary<string, string> EnvironmentFor(AgentKind kind);
}
