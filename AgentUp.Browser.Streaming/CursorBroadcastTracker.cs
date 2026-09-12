using System.Collections.Concurrent;

namespace AgentUp.Browser.Streaming;

/// <summary>
/// Decides when a workspace's viewers need to be told the cursor changed.
/// </summary>
/// <remarks>
/// Extracted from <see cref="BrowserInputDispatcher"/> because it is the only decision in
/// the mouse-move path that does not go through Puppeteer: the rest of that method is a
/// page call. A pointer move fires continuously, so broadcasting every reading would
/// flood every connected viewer with identical frames.
/// </remarks>
public sealed class CursorBroadcastTracker
{
    private readonly ConcurrentDictionary<string, string> _lastByWorkspace = new(StringComparer.Ordinal);

    /// <summary>
    /// Records the cursor for a workspace and reports whether it differs from the last one
    /// broadcast, so the first reading always goes out and repeats do not.
    /// </summary>
    public bool ShouldBroadcast(string workspaceId, string cursor)
    {
        var changed = !_lastByWorkspace.TryGetValue(workspaceId, out var last)
                      || !string.Equals(last, cursor, StringComparison.Ordinal);

        _lastByWorkspace[workspaceId] = cursor;
        return changed;
    }

    /// <summary>
    /// Forgets a workspace, so the next reading after a session ends is broadcast rather
    /// than suppressed by state from the previous session.
    /// </summary>
    public void Forget(string workspaceId) => _lastByWorkspace.TryRemove(workspaceId, out _);
}
