using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

// Linux/X11 adapter. Poking the top-level window alone isn't enough because the
// WebView's WebKitGTK surface lives in a *child* X window that GTK composites
// separately; parent-only Expose events don't cascade to it. So on every tick we
// walk the X window tree from the top-level and send XClearArea (with exposures=true)
// to every descendant window. GTK/WebKitGTK's paint machinery responds to those
// Expose events by re-compositing whichever descendant's surface changed — which is
// what actually gets fresh JPEG frames onto the screen while our window is unfocused.
//
// Own X display connection because Avalonia's isn't exposed publicly; cheap on Linux
// and correct because XClearArea targets the window ID, not connection-scoped state.
internal sealed class X11ViewerCompositorHint : IViewerCompositorHint, IDisposable
{
    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr displayName);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XClearArea(IntPtr display, IntPtr window, int x, int y, uint width, uint height, bool exposures);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XQueryTree(IntPtr display, IntPtr window, out IntPtr rootReturn,
        out IntPtr parentReturn, out IntPtr childrenReturn, out uint nchildrenReturn);

    [DllImport("libX11.so.6")]
    private static extern int XFree(IntPtr data);

    private readonly IntPtr _display;
    private bool _disposed;

    public X11ViewerCompositorHint()
    {
        _display = XOpenDisplay(IntPtr.Zero);
    }

    public void RequestRepaint(TopLevel window)
    {
        if (_disposed || _display == IntPtr.Zero) return;
        var handle = window.TryGetPlatformHandle();
        if (handle is null) return;
        if (!string.Equals(handle.HandleDescriptor, "XID", StringComparison.Ordinal)) return;

        try
        {
            ExposeRecursive(handle.Handle, depth: 0);
            XFlush(_display);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Trace.TraceWarning("X11ViewerCompositorHint disabled: libX11 unavailable ({0}).", ex.Message);
        }
    }

    // Walks descendants from `parent` and asks each to re-expose its whole client area.
    // Depth guard prevents runaway on a malformed tree; three levels is plenty for
    // Avalonia-top-level → GTK-plug → WebKitGTK-surface chains.
    private void ExposeRecursive(IntPtr parent, int depth)
    {
        XClearArea(_display, parent, 0, 0, 0, 0, exposures: true);
        if (depth >= 4) return;
        if (XQueryTree(_display, parent, out _, out _, out var children, out var nChildren) == 0) return;
        if (children == IntPtr.Zero || nChildren == 0) return;
        try
        {
            for (uint i = 0; i < nChildren; i++)
            {
                var child = Marshal.ReadIntPtr(children, (int)(i * IntPtr.Size));
                ExposeRecursive(child, depth + 1);
            }
        }
        finally
        {
            XFree(children);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_display != IntPtr.Zero) XCloseDisplay(_display);
    }
}
