using Avalonia.Controls;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

// Default adapter for platforms where the OS compositor is already reliable
// (Windows DWM, macOS Quartz). Both keep background windows composited so the WebView
// surface reaches the screen even while the app is unfocused.
internal sealed class NoopViewerCompositorHint : IViewerCompositorHint
{
    public void RequestRepaint(TopLevel window) { }
}
