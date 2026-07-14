using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AgentUp.Fixtures;

namespace AgentUp.Fixtures.Linux;

public sealed class LinuxDesktopFixtureAdapter : IDesktopFixtureAdapter
{
    private Process? _xvfb;

    public string Name => "AgentUp.Fixtures.Linux";
    public bool RequiresStaThread => false;
    public string StartupFailureHint => "Check that DISPLAY points at Xvfb or another reachable X server and that WebKitGTK native libraries are installed.";

    public void SetUp()
    {
        EnsureNativeLibraries();
        StartXvfb();
    }

    // On NixOS, native libraries live in the nix store and are only on LD_LIBRARY_PATH
    // when running inside `nix-shell`. IDEs launch the test host without that environment.
    // Changing LD_LIBRARY_PATH after process start does not propagate to in-process dlopen
    // on this NixOS setup, so pre-load the libraries by their explicit nix-store paths.
    private static void EnsureNativeLibraries()
    {
        if (NativeLibrary.TryLoad("libfontconfig.so.1", out var h))
        {
            NativeLibrary.Free(h);
            return;
        }

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

        var existing = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH",
            existing.Length > 0 ? $"{ldPath}:{existing}" : ldPath);

        foreach (var dir in ldPath.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.so.*"))
                NativeLibrary.TryLoad(file, out _);
        }
    }

    private static string? FindShellNix()
    {
        var dir = Path.GetDirectoryName(typeof(LinuxDesktopFixtureAdapter).Assembly.Location);
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
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS", "1");
        Environment.SetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE", "1");
        Environment.SetEnvironmentVariable("GALLIUM_DRIVER", "llvmpipe");
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_COMPOSITING_MODE", "1");
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_DMABUF_RENDERER", "1");

        if (Environment.GetEnvironmentVariable("DISPLAY") is not null && IsDisplayReady())
            return;

        _xvfb = Process.Start(new ProcessStartInfo
        {
            FileName = "Xvfb",
            Arguments = ":99 -screen 0 1280x720x24",
            UseShellExecute = false,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Failed to start Xvfb.");

        Environment.SetEnvironmentVariable("DISPLAY", ":99");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_xvfb.HasExited)
                throw new InvalidOperationException($"Xvfb exited before DISPLAY was ready: {_xvfb.StandardError.ReadToEnd()}");

            if (IsDisplayReady())
                return;

            Thread.Sleep(100);
        }

        throw new TimeoutException("Xvfb did not make DISPLAY=:99 available within 10 seconds.");
    }

    private static bool IsDisplayReady()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "xdpyinfo",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (proc is null)
                return false;

            proc.WaitForExit(1000);
            return proc.HasExited && proc.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            Thread.Sleep(500);
            return true;
        }
    }

    public void Dispose()
    {
        try { _xvfb?.Kill(); } catch { }
        _xvfb?.Dispose();
    }
}
