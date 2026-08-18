using AgentUp.Desktop.Features.Browser.Models;

namespace AgentUp.Desktop.Features.Workspaces.Services;

// Owns all viewer/stream-state per-workspace state that was previously scattered across
// MainWindow private fields. MainWindow calls into this store on every state change and
// reads back state when rendering or updating presence. All access is UI-thread-only
// (same contract as the MainWindow fields it replaces).
internal sealed class ViewerStateStore
{
    private readonly Dictionary<string, StreamStateSnapshot> _streamStates = new();
    private readonly Dictionary<string, StreamStateKind?> _lastRenderedKind = new();
    private readonly HashSet<string> _viewerPagesLoaded = new();
    private readonly Dictionary<string, ViewerSnapshot> _viewerSnapshots = new();
    private readonly Dictionary<string, string> _workspaceAuthority = new();

    // ── Stream state ─────────────────────────────────────────────────────────

    public StreamStateSnapshot? GetStreamState(string workspaceId)
        => _streamStates.GetValueOrDefault(workspaceId);

    public void SetStreamState(StreamStateSnapshot snapshot)
        => _streamStates[snapshot.WorkspaceId] = snapshot;

    // ── Last rendered kind ────────────────────────────────────────────────────

    public StreamStateKind? GetLastRenderedKind(string workspaceId)
        => _lastRenderedKind.GetValueOrDefault(workspaceId);

    public void SetLastRenderedKind(string workspaceId, StreamStateKind? kind)
        => _lastRenderedKind[workspaceId] = kind;

    // ── Viewer page loaded flag ───────────────────────────────────────────────

    public bool IsViewerPageLoaded(string workspaceId)
        => _viewerPagesLoaded.Contains(workspaceId);

    public void MarkViewerPageLoaded(string workspaceId)
        => _viewerPagesLoaded.Add(workspaceId);

    public void UnmarkViewerPageLoaded(string workspaceId)
        => _viewerPagesLoaded.Remove(workspaceId);

    // ── Viewer JS snapshot ────────────────────────────────────────────────────

    public ViewerSnapshot? GetViewerSnapshot(string workspaceId)
        => _viewerSnapshots.GetValueOrDefault(workspaceId);

    public void SetViewerSnapshot(string workspaceId, ViewerSnapshot snapshot)
        => _viewerSnapshots[workspaceId] = snapshot;

    public void RemoveViewerSnapshot(string workspaceId)
        => _viewerSnapshots.Remove(workspaceId);

    // ── Control authority ("ai" | "human") ───────────────────────────────────

    public string GetAuthority(string workspaceId)
        => _workspaceAuthority.GetValueOrDefault(workspaceId, "ai");

    public void SetAuthority(string workspaceId, string authority)
        => _workspaceAuthority[workspaceId] = authority;

    // ── Bulk lifecycle operations ─────────────────────────────────────────────

    // Remove all viewer state for one workspace (called when a workspace is removed from the sidebar).
    // Does NOT remove _viewerSnapshots: that is cleared by RenderStreamState when the viewer WebView
    // is destroyed, which is the correct moment (the snapshot is stale once the WebView is gone).
    public void RemoveWorkspace(string workspaceId)
    {
        _streamStates.Remove(workspaceId);
        _lastRenderedKind.Remove(workspaceId);
        _viewerPagesLoaded.Remove(workspaceId);
        _workspaceAuthority.Remove(workspaceId);
    }

    // Clear only the stream / render-state dicts. Called on SSE disconnect: server-side truth
    // is unknown, so we hide all viewers until reconnect replays events.
    public void ClearStreamStates()
    {
        _streamStates.Clear();
        _lastRenderedKind.Clear();
    }

    // Clear all state. Called on window close or full workspace collection reset.
    public void ClearAll()
    {
        _streamStates.Clear();
        _lastRenderedKind.Clear();
        _viewerPagesLoaded.Clear();
        _viewerSnapshots.Clear();
        _workspaceAuthority.Clear();
    }

    // True only when every dict is empty — used by HasWorkspaceBrowserResourcesForTests.
    public bool IsEmpty =>
        _streamStates.Count == 0
        && _lastRenderedKind.Count == 0
        && _viewerPagesLoaded.Count == 0
        && _viewerSnapshots.Count == 0
        && _workspaceAuthority.Count == 0;
}
