using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>
/// The ACP sessions a workspace can come back to once the process that served them is gone.
/// Sessions are partitioned by workspace, so one workspace never sees another's history.
/// </summary>
public interface IAgentSessionRepository
{
    IReadOnlyList<PersistedAgentSession> List(string workspaceId);
    PersistedAgentSession? Find(string workspaceId, string sessionId);
    void Upsert(PersistedAgentSession session);

    /// <summary>Moves a saved session to the id the agent handed back, leaving no duplicate behind.</summary>
    void Rekey(PersistedAgentSession session, string previousSessionId);

    void RemoveWorkspace(string workspaceId);
}
