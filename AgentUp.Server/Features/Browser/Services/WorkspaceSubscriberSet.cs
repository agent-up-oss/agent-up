using System.Net.WebSockets;

namespace AgentUp.Server.Features.Browser.Services;

internal sealed class WorkspaceSubscriberSet
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, WebSocket> _sockets = [];

    public void Add(Guid id, WebSocket ws) { lock (_lock) _sockets[id] = ws; }
    public void Remove(Guid id) { lock (_lock) _sockets.Remove(id); }
    public WebSocket[] Snapshot() { lock (_lock) return [.. _sockets.Values]; }
}
