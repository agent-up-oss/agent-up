using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>The origin a sandboxed model request is forwarded to.</summary>
public interface IAgentModelProxyDestinations
{
    AgentModelProxyTarget? Resolve(string capability);
}
