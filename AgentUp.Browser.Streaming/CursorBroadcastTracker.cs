namespace AgentUp.Browser.Streaming;

/// <summary>
/// Decides when a session's viewers need to be told the cursor changed.
/// </summary>
/// <remarks>
/// Owned by <see cref="Models.BrowserSessionState"/>, so one of these lives and dies with
/// the session it belongs to. That is deliberate: as a singleton keyed by workspace id it
/// held the last cursor of a session that had already been disposed, and a later session on
/// the same workspace would have its first cursor frame suppressed for reporting the same
/// value. Per-session state cannot outlive its session, so there is nothing to remember to
/// clear on teardown.
/// <para>
/// Extracted from <see cref="BrowserInputDispatcher"/> because it is the only decision in
/// the mouse-move path that does not go through Puppeteer: the rest of that method is a
/// page call. A pointer move fires continuously, so broadcasting every reading would flood
/// every connected viewer with identical frames.
/// </para>
/// </remarks>
public sealed class CursorBroadcastTracker
{
    private readonly object _gate = new();
    private string? _last;

    /// <summary>
    /// Records a cursor and reports whether it differs from the last one broadcast, so the
    /// first reading always goes out and repeats do not.
    /// </summary>
    public bool ShouldBroadcast(string cursor)
    {
        lock (_gate)
        {
            var changed = !string.Equals(_last, cursor, StringComparison.Ordinal);
            _last = cursor;
            return changed;
        }
    }
}
