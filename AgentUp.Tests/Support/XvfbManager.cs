using System.Diagnostics;
using System.Runtime.InteropServices;
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
        EnsureNativeLibraries();
        StartXvfb();
        StartAvalonia();
    }

    // On NixOS, native libraries (SkiaSharp, GTK, WebKit) live in the nix store and
    // are only on LD_LIBRARY_PATH when running inside `nix-shell`. Rider and other IDEs
    // launch the test host process without that environment, so we bootstrap it here by
    // asking nix-shell for the correct paths before Avalonia/SkiaSharp are loaded.
    // On NixOS, native libraries live in the nix store and are only on LD_LIBRARY_PATH
    // when running inside `nix-shell`. IDEs launch the test host without that environment.
    // Changing LD_LIBRARY_PATH after process start doesn't propagate to in-process dlopen
    // on this NixOS setup, so we pre-load the libraries by their explicit nix-store paths.
    // Nix-store libs have embedded rpath, so their transitive deps (freetype, bzip2, …)
    // resolve automatically once the top-level lib is loaded. The LD_LIBRARY_PATH update
    // is still needed for child processes (Xvfb, WebKit network/web workers).
    private static void EnsureNativeLibraries()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        if (NativeLibrary.TryLoad("libfontconfig.so.1", out var h)) { NativeLibrary.Free(h); return; }

        var shellNix = FindShellNix();
        if (shellNix is null) return;

        var psi = new ProcessStartInfo
        {
            FileName = "nix-shell",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(shellNix);
        psi.ArgumentList.Add("--run");
        psi.ArgumentList.Add("echo $LD_LIBRARY_PATH");

        using var proc = Process.Start(psi);
        if (proc is null) return;

        var ldPath = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit();
        if (string.IsNullOrEmpty(ldPath)) return;

        // Update LD_LIBRARY_PATH so child processes (Xvfb, WebKit workers) inherit the paths.
        var existing = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH",
            existing.Length > 0 ? $"{ldPath}:{existing}" : ldPath);

        // Pre-load every versioned .so from the nix-shell lib dirs by explicit full path.
        // Nix-store libs carry embedded rpath entries pointing at their own transitive deps,
        // so each pre-loaded lib resolves its own dependencies without needing LD_LIBRARY_PATH.
        // Once a lib is in the process's library cache (keyed by SONAME), later dlopen calls
        // (from SkiaSharp, GTK, WebKit, …) find the already-loaded copy automatically.
        foreach (var dir in ldPath.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.so.*"))
                NativeLibrary.TryLoad(file, out _);
        }
    }

    private static string? FindShellNix()
    {
        var dir = Path.GetDirectoryName(typeof(XvfbManager).Assembly.Location);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "shell.nix");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
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
