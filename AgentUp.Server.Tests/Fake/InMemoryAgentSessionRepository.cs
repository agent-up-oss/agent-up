using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Fake;

/// <summary>
/// The saved-session store without a file behind it, so unit tests can assert on what the
/// scheduler persisted without touching the filesystem.
/// </summary>
internal sealed class InMemoryAgentSessionRepository : IAgentSessionRepository
{
    private readonly List<PersistedAgentSession> _sessions = [];

    public IReadOnlyList<PersistedAgentSession> List(string workspaceId)
    {
        lock (_sessions)
            return _sessions.Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.LastUsedAt).ToArray();
    }

    public PersistedAgentSession? Find(string workspaceId, string sessionId) =>
        List(workspaceId).FirstOrDefault(item => item.SessionId == sessionId);

    public void Upsert(PersistedAgentSession session)
    {
        lock (_sessions)
        {
            _sessions.RemoveAll(item => item.WorkspaceId == session.WorkspaceId && item.SessionId == session.SessionId);
            _sessions.Add(session);
        }
    }

    public void Rekey(PersistedAgentSession session, string previousSessionId)
    {
        lock (_sessions)
        {
            _sessions.RemoveAll(item => item.WorkspaceId == session.WorkspaceId
                && (item.SessionId == previousSessionId || item.SessionId == session.SessionId));
            _sessions.Add(session);
        }
    }

    public void RemoveWorkspace(string workspaceId)
    {
        lock (_sessions)
            _sessions.RemoveAll(item => item.WorkspaceId == workspaceId);
    }
}
