namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IEnabledAgentInspector
{
    bool SupportsPermissionRequests(string agentModuleId);
}
