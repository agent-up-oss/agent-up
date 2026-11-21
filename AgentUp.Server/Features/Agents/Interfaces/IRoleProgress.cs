using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IRoleProgress
{
    void Record(string workspaceId, RoleTerminationKind kind);

    bool Satisfied(string workspaceId, RoleTerminationKind kind);

    /// <summary>Forgets what happened on the last task, so each task's termination is its own.</summary>
    void Reset(string workspaceId);
}
