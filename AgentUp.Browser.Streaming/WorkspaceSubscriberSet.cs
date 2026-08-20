using System.Net.WebSockets;

namespace AgentUp.Browser.Streaming;

public enum PresenceState
{
    Foreground,
    Background,
}

internal sealed class SubscriberEntry(WebSocket socket)
{
    public WebSocket Socket { get; } = socket;
    public PresenceState Presence { get; set; } = PresenceState.Foreground;
    // UtcTicks of last frame sent — used to enforce the 1 fps cap for background
    // subscribers so a busy foreground viewer never speeds up an inactive one.
    public long LastFrameSentAtTicks { get; set; }
}

public sealed class WorkspaceSubscriberSet
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, SubscriberEntry> _entries = [];

    public bool IsEmpty { get { lock (_lock) return _entries.Count == 0; } }

    public void Add(Guid id, WebSocket ws)
    {
        lock (_lock) _entries[id] = new SubscriberEntry(ws);
    }

    public void Remove(Guid id)
    {
        lock (_lock) _entries.Remove(id);
    }

    public bool SetPresence(Guid id, PresenceState state)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(id, out var entry)) return false;
            var changed = entry.Presence != state;
            entry.Presence = state;
            return changed;
        }
    }

    public bool HasForeground()
    {
        lock (_lock) return _entries.Values.Any(entry => entry.Presence == PresenceState.Foreground);
    }

    internal SubscriberEntry[] Snapshot()
    {
        lock (_lock) return [.. _entries.Values];
    }
}
