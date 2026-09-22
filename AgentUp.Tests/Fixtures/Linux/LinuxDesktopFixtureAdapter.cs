using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AgentUp.Tests.Fixtures.Linux;

public sealed class LinuxDesktopFixtureAdapter : IDesktopFixtureAdapter
{
    internal const string IsolatedWaylandDisplayName = "agentup-e2e-no-wayland";
    internal static string? IsolatedDisplay { get; private set; }
    private const string IsolatedFlag = "AGENTUP_E2E_DISPLAY_ISOLATED";

    private static readonly object Gate = new();
    private static Process? Xvfb;
    private static Process? Dbus;
    private static DirectoryInfo? RuntimeDir;
    private static bool Isolated;

    public string Name => "AgentUp.Fixtures.Linux";
    public bool RequiresStaThread => false;
    public bool RequiresSetupThreadAvalonia => false;
    public string StartupFailureHint => "The Linux fixture starts a private Xvfb display and XDG_RUNTIME_DIR, and imports PATH/libraries from nix-shell shell.nix so IDEs do not need extra env vars.";

    public void SetUp() => EnsureIsolated();

    // Rider/VSTest load this assembly without nix-shell. Isolate and import native
    // libraries here, before Avalonia or WebKitGTK initialize against the session.
    internal static void EnsureIsolated()
    {
        if (!OperatingSystem.IsLinux())
            return;

        lock (Gate)
        {
            if (Isolated)
                return;

            ImportNixShellEnvironment();
            if (UseSessionDisplay())
            {
                Environment.SetEnvironmentVariable("WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS", "1");
                PreloadNativeLibraries();
                Isolated = true;
                return;
            }

            IsolateFromSessionDesktop();
            StartPrivateDbus();
            StartXvfb();
            PreloadNativeLibraries();
            Environment.SetEnvironmentVariable(IsolatedFlag, "1");
            Isolated = true;
        }
    }

    internal const string ImportNixFlag = "AGENTUP_E2E_IMPORT_NIX_SHELL";

    /// <summary>
    /// Whether this host's GTK, WebKit and Skia libraries have to come from <c>shell.nix</c>
    /// rather than from the system loader.
    /// </summary>
    /// <remarks>
    /// Importing the Nix closure is a NixOS accommodation: there the system loader has no GTK or
    /// WebKit to find, so the fixture puts that closure on <c>LD_LIBRARY_PATH</c> and preloads it.
    /// On a host that ships those libraries itself the import is not redundant but fatal - the
    /// closure is linked against its own glibc, and mapping it beside the system copies Avalonia
    /// has already loaded ends the process with SIGSEGV at whatever point the second copy is first
    /// touched, which is why the crash moved between runs instead of naming one call. Nix merely
    /// being installed is not the question: the runtime capability job installs it so capability
    /// launches can wrap through it, and that runner's desktop still belongs to Ubuntu.
    /// </remarks>
    internal static bool ShouldImportNixEnvironment(Func<string, bool> fileExists, Func<string, string?> environment)
    {
        var requested = environment(ImportNixFlag);
        if (!string.IsNullOrWhiteSpace(requested))
            return requested.Trim() is "1" || bool.TryParse(requested.Trim(), out var parsed) && parsed;

        return fileExists("/etc/NIXOS");
    }

    // IDEs launch the testhost without nix-shell. System libfontconfig often loads, so we
    // must not treat that as "native libraries are ready" — WebKitGTK/GTK still need the
    // nix store paths from shell.nix. Changing LD_LIBRARY_PATH after process start does not
    // propagate to in-process dlopen on this NixOS setup, so preload by absolute path.
    private static void ImportNixShellEnvironment()
    {
        if (!ShouldImportNixEnvironment(File.Exists, Environment.GetEnvironmentVariable))
            return;

        var shellNix = FindShellNix();
        if (shellNix is null)
            return;

        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "nix-shell",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList =
                {
                    shellNix,
                    "--run",
                    "printf '__AGENTUP_NIX_PATH__%s\\n__AGENTUP_NIX_LD__%s\\n' \"$PATH\" \"$LD_LIBRARY_PATH\""
                }
            });
            if (proc is null)
                return;

            var stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            ApplyNixValue(stdout, "__AGENTUP_NIX_PATH__", "PATH");
            ApplyNixValue(stdout, "__AGENTUP_NIX_LD__", "LD_LIBRARY_PATH");
        }
        catch (Win32Exception ex)
        {
            // Ubuntu CI and hosts without Nix keep using the system WebKit/Xvfb packages.
            Trace.TraceWarning(ex.Message);
        }
    }

    private static void ApplyNixValue(string stdout, string marker, string variable)
    {
        var line = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(value => value.StartsWith(marker, StringComparison.Ordinal));
        if (line is null)
            return;

        var imported = line[marker.Length..];
        if (string.IsNullOrWhiteSpace(imported))
            return;

        var existing = Environment.GetEnvironmentVariable(variable) ?? "";
        Environment.SetEnvironmentVariable(variable,
            existing.Length > 0 ? $"{imported}:{existing}" : imported);
    }

    private static void PreloadNativeLibraries()
    {
        var ldPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
        if (string.IsNullOrWhiteSpace(ldPath))
            return;

        foreach (var dir in ldPath.Split(':', StringSplitOptions.RemoveEmptyEntries).Where(Directory.Exists))
        {
            foreach (var file in Directory.GetFiles(dir, "*.so.*"))
            {
                var name = Path.GetFileName(file);
                if (name.Contains("asan", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("tsan", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("ubsan", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("msan", StringComparison.OrdinalIgnoreCase))
                    continue;

                NativeLibrary.TryLoad(file, out _);
            }
        }
    }

    private static string? FindShellNix()
    {
        var dir = Path.GetDirectoryName(typeof(LinuxDesktopFixtureAdapter).Assembly.Location);
        while (dir is not null)
        {
            var candidate = Path.Join(dir, "shell.nix");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }

        return null;
    }

    // A local workstation already has DISPLAY (and often WAYLAND_DISPLAY plus a session
    // D-Bus). Reusing those opens real Desktop/Installer windows and xdg-desktop-portal
    // file choosers on the developer's screen. Always isolate unless a maintainer opts
    // back into the session for visual debugging.
    private static bool UseSessionDisplay()
        => Environment.GetEnvironmentVariable("AGENTUP_E2E_USE_SESSION_DISPLAY") == "1"
           && Environment.GetEnvironmentVariable("DISPLAY") is not null
           && IsDisplayReady();

    private static void IsolateFromSessionDesktop()
    {
        RuntimeDir = Directory.CreateTempSubdirectory("agentup-e2e-");
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                RuntimeDir.FullName,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", RuntimeDir.FullName);
        // Unsetting WAYLAND_DISPLAY is not enough: GTK and Avalonia then use
        // $XDG_RUNTIME_DIR/wayland-0, which is the session compositor. Point at a
        // name that cannot exist in the private runtime dir instead.
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", IsolatedWaylandDisplayName);
        Environment.SetEnvironmentVariable("WAYLAND_SOCKET", null);
        Environment.SetEnvironmentVariable("DISPLAY", null);
        Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
        Environment.SetEnvironmentVariable("XDG_SESSION_TYPE", "x11");
        Environment.SetEnvironmentVariable("GTK_USE_PORTAL", "0");
        Environment.SetEnvironmentVariable("GSETTINGS_BACKEND", "memory");
        Environment.SetEnvironmentVariable("NO_AT_BRIDGE", "1");
        Environment.SetEnvironmentVariable("GTK_A11Y", "none");
        Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", null);
        Environment.SetEnvironmentVariable("DESKTOP_SESSION", null);
    }

    private static void StartPrivateDbus()
    {
        var busPath = Path.Join(RuntimeDir!.FullName, "bus");
        var address = "unix:path=" + busPath;
        // Leave the address set even if dbus-daemon is missing so libdbus cannot
        // fall back to the session $XDG_RUNTIME_DIR/bus (or the previous one).
        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", address);
        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_PID", null);

        try
        {
            Dbus = Process.Start(new ProcessStartInfo
            {
                FileName = "dbus-daemon",
                ArgumentList = { "--session", "--nofork", "--nopidfile", "--address=" + address },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
        }
        catch (Win32Exception ex)
        {
            Trace.TraceWarning(ex.Message);
            return;
        }

        if (Dbus is null)
            return;

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (Dbus.HasExited)
                return;

            if (File.Exists(busPath))
                return;

            Thread.Sleep(50);
        }
    }

    private static void StartXvfb()
    {
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS", "1");
        Environment.SetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE", "1");
        Environment.SetEnvironmentVariable("GALLIUM_DRIVER", "llvmpipe");
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_COMPOSITING_MODE", "1");
        Environment.SetEnvironmentVariable("WEBKIT_DISABLE_DMABUF_RENDERER", "1");

        Exception? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var display = $":{Random.Shared.Next(100, 60_000)}";
            try
            {
                Xvfb = StartXvfbProcess(display);
                IsolatedDisplay = display;
                Environment.SetEnvironmentVariable("DISPLAY", display);
                WaitUntilXvfbReady(display);
                return;
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or Win32Exception)
            {
                last = ex;
                TryKill(Xvfb);
                Xvfb?.Dispose();
                Xvfb = null;
            }
        }

        throw new InvalidOperationException(
            "Failed to start a private Xvfb display for Linux E2E. Install xorg-x11-server-Xvfb or enter nix-shell shell.nix.",
            last);
    }

    private static Process StartXvfbProcess(string display)
    {
        Process process;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = "Xvfb",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { display, "-screen", "0", "1280x720x24", "-nolisten", "tcp" }
            }) ?? throw new InvalidOperationException("Failed to start Xvfb.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Failed to start Xvfb. Install xorg-x11-server-Xvfb or enter nix-shell shell.nix.", ex);
        }

        return process;
    }

    private static void WaitUntilXvfbReady(string display)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (Xvfb is { HasExited: true })
                throw new InvalidOperationException($"Xvfb exited before DISPLAY was ready: {Xvfb.StandardError.ReadToEnd()}");

            if (IsDisplayReady())
                return;

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Xvfb did not make DISPLAY={display} available within 10 seconds.");
    }

    private static void TryKill(Process? process)
    {
        try { process?.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { Trace.TraceWarning(ex.Message); }
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

            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { Trace.TraceWarning(ex.Message); }
                return false;
            }

            Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
            return proc.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            Thread.Sleep(500);
            return true;
        }
    }

    public void Dispose() => DisposeIsolation();

    internal static void DisposeIsolation()
    {
        lock (Gate)
        {
            try { Xvfb?.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { Trace.TraceWarning(ex.Message); }
            Xvfb?.Dispose();
            Xvfb = null;
            try { Dbus?.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { Trace.TraceWarning(ex.Message); }
            Dbus?.Dispose();
            Dbus = null;
            try { RuntimeDir?.Delete(recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Trace.TraceWarning(ex.Message); }
            RuntimeDir = null;
            Isolated = false;
            IsolatedDisplay = null;
        }
    }
}
