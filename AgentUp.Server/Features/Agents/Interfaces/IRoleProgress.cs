using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IRoleProgress
{
    void Record(string workspaceId, RoleTerminationKind kind);

    bool Satisfied(string workspaceId, RoleTerminationKind kind);
}
