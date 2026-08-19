using System.Runtime.InteropServices;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

// Picks the compositor adapter for the current OS at startup. The X11 adapter is
// used whenever we're on Linux AND the DISPLAY env var is set (a good proxy for
// X11 vs Wayland — Wayland falls back to the noop until we add a proper adapter).
internal static class ViewerCompositorHintFactory
{
    public static IViewerCompositorHint Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            return new X11ViewerCompositorHint();
        }
        return new NoopViewerCompositorHint();
    }
}
