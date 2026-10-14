using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class EnabledAgentInspector(IEnabledCapabilityPackages? packages = null) : IEnabledAgentInspector
{
    public bool SupportsPermissionRequests(string agentModuleId)
    {
        var agent = packages?.GetAgent(agentModuleId);
        return agent?.SupportsPermissionRequests ?? true;
    }
}
