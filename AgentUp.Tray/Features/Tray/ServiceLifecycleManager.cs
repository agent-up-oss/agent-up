using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;

namespace AgentUp.Tray.Features.Tray;

public sealed class ServiceLifecycleManager : IDisposable
{
    private readonly BehaviorSubject<ServiceState> _state = new(ServiceState.Stopped);
    private readonly string _serverBinary;
    private readonly object _lock = new();
    private Process? _serverProcess;
    private bool _intentionallyStopped;
    private int _crashCount;
    private CancellationTokenSource _restartCts = new();

    public IObservable<ServiceState> State => _state;
    public ServiceState CurrentState => _state.Value;

    public ServiceLifecycleManager()
    {
        _serverBinary = ResolveServerBinary();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _intentionallyStopped = false;
        _crashCount = 0;
        await SpawnAsync(ct);
    }

    public async Task PauseAsync()
    {
        CancellationTokenSource oldCts;
        lock (_lock)
        {
            _intentionallyStopped = true;
            oldCts = _restartCts;
            _restartCts = new CancellationTokenSource();
        }
        oldCts.Cancel();
        oldCts.Dispose();
        _state.OnNext(ServiceState.Paused);
        await StopServerAsync();
    }

    public async Task ResumeAsync()
    {
        _crashCount = 0;
        _intentionallyStopped = false;
        await SpawnAsync();
    }

    public async Task RestartAsync()
    {
        _crashCount = 0;
        _intentionallyStopped = false;
        _state.OnNext(ServiceState.Restarting);
        await StopServerAsync();
        await SpawnAsync();
    }

    public async Task QuitAsync()
    {
        CancellationTokenSource oldCts;
        lock (_lock)
        {
            _intentionallyStopped = true;
            oldCts = _restartCts;
            _restartCts = new CancellationTokenSource();
        }
        oldCts.Cancel();
        oldCts.Dispose();
        await StopServerAsync();
    }

    private async Task SpawnAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_intentionallyStopped) return;
        }

        _state.OnNext(ServiceState.Starting);
        try
        {
            var startInfo = new ProcessStartInfo(_serverBinary)
            {
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };
            startInfo.Environment["AGENTUP_TRAY_PID"] = Environment.ProcessId.ToString();

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Exited += OnServerExited;
            process.Start();

            lock (_lock)
            {
                _serverProcess = process;
            }
            _state.OnNext(ServiceState.Running);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Trace.TraceError($"Failed to start AgentUp.Server: {ex.Message}");
            _state.OnNext(ServiceState.Failed);
        }
    }

    private void OnServerExited(object? sender, EventArgs e)
    {
        bool intentional;
        CancellationToken restartToken;
        lock (_lock)
        {
            intentional = _intentionallyStopped;
            if (!intentional) _crashCount++;
            restartToken = intentional ? CancellationToken.None : _restartCts.Token;
        }

        if (intentional) return;

        var backoff = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, _crashCount - 1), 30));
        _state.OnNext(ServiceState.Restarting);

        _ = Task.Delay(backoff, restartToken)
            .ContinueWith(t => { if (!t.IsCanceled) _ = SpawnAsync(); }, TaskScheduler.Default);
    }

    private async Task StopServerAsync()
    {
        Process? process;
        lock (_lock)
        {
            process = _serverProcess;
            _serverProcess = null;
        }

        if (process is null || process.HasExited)
        {
            process?.Dispose();
            _state.OnNext(ServiceState.Stopped);
            return;
        }

        process.Exited -= OnServerExited;

        using (process)
        {
            try
            {
                await SendGracefulStopAsync(process);
            }
            catch (InvalidOperationException)
            {
                // Process already exited between the HasExited check and SendGracefulStopAsync
            }
            catch (Win32Exception ex)
            {
                Trace.TraceError($"Failed to stop server process {process.Id}: {ex.Message}");
            }
        }

        _state.OnNext(ServiceState.Stopped);
    }

    private static async Task SendGracefulStopAsync(Process p)
    {
        if (!OperatingSystem.IsWindows())
        {
            // Ask server to shut down gracefully via SIGTERM; ConsoleLifetime in ASP.NET Core handles it
            NativeMethods.SendSigterm(p.Id);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try { await p.WaitForExitAsync(cts.Token); return; }
            catch (OperationCanceledException) { /* fall through to force kill */ }
        }

        p.Kill(entireProcessTree: true);
        await p.WaitForExitAsync();
    }

    private static string ResolveServerBinary()
    {
        var exe = OperatingSystem.IsWindows() ? "AgentUp.Server.exe" : "AgentUp.Server";

        // Installed layout: tray and server are siblings under a shared root (e.g. /opt/agent-up/tray/ and /opt/agent-up/server/)
        var sibling = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "server", exe));
        if (File.Exists(sibling)) return sibling;

        // Dev layout: all output goes to a single bin directory
        var dev = Path.Join(AppContext.BaseDirectory, exe);
        if (File.Exists(dev)) return dev;

        throw new FileNotFoundException(
            $"Cannot locate {exe} relative to '{AppContext.BaseDirectory}'. " +
            "Expected it at ../server/ (installed) or in the same directory (dev).");
    }

    public void Dispose()
    {
        _state.Dispose();
        _restartCts.Dispose();
        _serverProcess?.Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        private static extern int KillPosix(int pid, int sig);

        internal static void SendSigterm(int pid)
        {
            if (!OperatingSystem.IsWindows())
                KillPosix(pid, 15); // SIGTERM
        }
    }
}
