using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace AgentUp.Tests;

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
        // WebKit's bwrap-based process sandbox can fail in headless/CI environments.
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS", "1");

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

    private static void StartAvalonia()
    {
        var thread = new Thread(() =>
        {
            AppBuilder.Configure<E2ETestApp>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .AfterSetup(_ =>
                {
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
