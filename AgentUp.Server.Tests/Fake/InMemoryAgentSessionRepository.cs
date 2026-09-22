using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Fake;

internal sealed class InMemoryAgentSessionRepository : IAgentSessionRepository
{
    private readonly List<PersistedAgentSession> _sessions = [];

    public IReadOnlyList<PersistedAgentSession> List(string workspaceId) =>
        _sessions.Where(item => item.WorkspaceId == workspaceId).OrderByDescending(item => item.LastUsedAt).ToArray();

    public PersistedAgentSession? Find(string workspaceId, string sessionId) =>
        _sessions.FirstOrDefault(item => item.WorkspaceId == workspaceId && item.SessionId == sessionId);

    public void Upsert(PersistedAgentSession session)
    {
        _sessions.RemoveAll(item => item.WorkspaceId == session.WorkspaceId && item.SessionId == session.SessionId);
        _sessions.Add(session);
    }

    public void RemoveWorkspace(string workspaceId) => _sessions.RemoveAll(item => item.WorkspaceId == workspaceId);
}
