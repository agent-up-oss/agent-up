using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>Persists ACP session identities separately from their short-lived processes.</summary>
public sealed class AgentSessionRepository : IAgentSessionRepository
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _path;
    private readonly object _gate = new();

    public AgentSessionRepository(string dataDirectory) => _path = Path.Join(dataDirectory, "agent-sessions.json");

    public IReadOnlyList<PersistedAgentSession> List(string workspaceId)
    {
        lock (_gate)
            return Read().Where(item => item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.LastUsedAt).ToArray();
    }

    public PersistedAgentSession? Find(string workspaceId, string sessionId) =>
        List(workspaceId).FirstOrDefault(item => item.SessionId == sessionId);

    public void Upsert(PersistedAgentSession session)
    {
        lock (_gate)
        {
            var sessions = Read().Where(item => !(item.WorkspaceId == session.WorkspaceId && item.SessionId == session.SessionId)).ToList();
            sessions.Add(session);
            Write(sessions);
        }
    }

    public void Rekey(PersistedAgentSession session, string previousSessionId)
    {
        lock (_gate)
        {
            var sessions = Read().Where(item => item.WorkspaceId != session.WorkspaceId
                || (item.SessionId != previousSessionId && item.SessionId != session.SessionId)).ToList();
            sessions.Add(session);
            Write(sessions);
        }
    }

    public void RemoveWorkspace(string workspaceId)
    {
        lock (_gate)
        {
            var sessions = Read().Where(item => item.WorkspaceId != workspaceId).ToList();
            if (File.Exists(_path)) Write(sessions);
        }
    }

    private List<PersistedAgentSession> Read()
    {
        if (!File.Exists(_path)) return [];
        try { return JsonSerializer.Deserialize<List<PersistedAgentSession>>(File.ReadAllText(_path), Options) ?? []; }
        catch (JsonException) { return []; }
    }

    private void Write(IReadOnlyList<PersistedAgentSession> sessions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(sessions, Options));
        File.Move(temporary, _path, true);
    }
}
