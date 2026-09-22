using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAgentSessionRepository
{
    IReadOnlyList<PersistedAgentSession> List(string workspaceId);
    PersistedAgentSession? Find(string workspaceId, string sessionId);
    void Upsert(PersistedAgentSession session);
    void RemoveWorkspace(string workspaceId);
}
