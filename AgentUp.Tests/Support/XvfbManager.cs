using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.WebView.Desktop;
using AvaloniaWebView;

namespace AgentUp.Tests;

// Starts a virtual X server and the Avalonia platform before any test runs.
// Both Xvfb and Avalonia are started with explicit timeouts so a hang is
// surfaced as a test failure rather than a silent freeze.
[SetUpFixture]
public sealed class XvfbManager
{
    private Process? _xvfb;
    private static readonly ManualResetEventSlim _avaloniaReady = new(false);

    [OneTimeSetUp]
    public void Start()
    {
        StartXvfb();
        StartAvalonia();
    }

    private void StartXvfb()
    {
        if (Environment.GetEnvironmentVariable("DISPLAY") is not null)
            return;

        _xvfb = Process.Start(new ProcessStartInfo
        {
            FileName = "Xvfb",
            Arguments = ":99 -screen 0 1280x720x24",
            UseShellExecute = false,
            RedirectStandardError = true,
        });

        Environment.SetEnvironmentVariable("DISPLAY", ":99");
        Thread.Sleep(500);
    }

    // Pre-initialize the WebKit default context so that NetworkProcess has been
    // started and connected before the GLib main loop begins. Calling this from
    // AfterSetup (before StartWithClassicDesktopLifetime enters g_main_loop_run)
    // allows WebKit to spin g_main_context_iteration() internally to wait for the
    // process IPC handshake — something it cannot do from within a dispatcher
    // callback because that would require the very same main context it already owns.
    [DllImport("libwebkit2gtk-4.1.so.0")]
    private static extern IntPtr webkit_web_context_get_default();

    private static void PreWarmWebKit()
    {
        try
        {
            NavLog("PreWarmWebKit: calling webkit_web_context_get_default");
            _ = webkit_web_context_get_default();
            NavLog("PreWarmWebKit: webkit_web_context_get_default returned");
        }
        catch (Exception ex)
        {
            NavLog($"PreWarmWebKit: FAILED {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Pre-run the AvaloniaWebView.WebView static constructor and exercise the
    // instance constructor before the GLib main loop starts. This lets any
    // blocking one-time initialization (WebKit process spawning, etc.) complete
    // with free access to g_main_context_iteration rather than deadlocking inside
    // a GLib dispatch callback.
    private static void PreWarmWebViewType()
    {
        try
        {
            NavLog("PreWarmWebViewType: creating warmup WebView");
            var wv = new WebView();
            NavLog("PreWarmWebViewType: warmup WebView created successfully");
        }
        catch (Exception ex)
        {
            NavLog($"PreWarmWebViewType: FAILED {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void NavLog(string msg) =>
        System.IO.File.AppendAllText("/tmp/agentup-nav.log", $"[XvfbMgr] {msg}\n");

    private static void StartAvalonia()
    {
        var thread = new Thread(() =>
        {
            AppBuilder.Configure<E2ETestApp>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .UseDesktopWebView()
                .AfterSetup(_ =>
                {
                    PreWarmWebKit();
                    PreWarmWebViewType();
                    Dispatcher.UIThread.Post(() => _avaloniaReady.Set());
                })
                .StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        });
        thread.IsBackground = true;
        thread.Start();

        if (!_avaloniaReady.Wait(TimeSpan.FromSeconds(60)))
            throw new TimeoutException("Avalonia platform failed to initialize within 60 seconds. " +
                "Check that DISPLAY is set and Xvfb / a real X server is reachable.");
    }

    [OneTimeTearDown]
    public void Stop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
                lt.Shutdown();
        });

        try { _xvfb?.Kill(); } catch { /* already gone */ }
        _xvfb?.Dispose();
    }
}

file sealed class E2ETestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
