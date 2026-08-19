using Avalonia.Controls;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

// Platform-specific hint to nudge the OS window compositor into re-blitting the viewer
// WebView's back buffer to the screen. Needed on Linux/X11 because when the AgentUp
// window is unfocused most WMs stop asking WebKit for its surface — the JS keeps
// drawing frames into the canvas but they never land on the screen.
//
// Windows and macOS composite background windows aggressively by default, so their
// implementations are a noop. Wayland is deferred (its damage model differs; the
// noop is a safe default until we add a proper protocol adapter).
internal interface IViewerCompositorHint
{
    // Called periodically by the snapshot poller. Implementations should be cheap and
    // best-effort — a failed poke on one tick is not fatal, the next tick will retry.
    void RequestRepaint(TopLevel window);
}
