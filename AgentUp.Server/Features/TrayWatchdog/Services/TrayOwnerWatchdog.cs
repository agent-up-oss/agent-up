using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.TrayWatchdog.Services;

// Shuts down the server if the tray process that owns it dies.
// Only active when AGENTUP_TRAY_PID is set in the environment (i.e. started by AgentUp.Tray).
public sealed class TrayOwnerWatchdog : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly Func<string, string?> _envReader;
    private CancellationTokenSource? _cts;

    public TrayOwnerWatchdog(IHostApplicationLifetime lifetime)
        : this(lifetime, Environment.GetEnvironmentVariable) { }

    internal TrayOwnerWatchdog(IHostApplicationLifetime lifetime, Func<string, string?> envReader)
    {
        _lifetime = lifetime;
        _envReader = envReader;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var pidEnv = _envReader("AGENTUP_TRAY_PID");
        if (!int.TryParse(pidEnv, out var trayPid))
            return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = MonitorAsync(trayPid, _cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task MonitorAsync(int trayPid, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { return; }

            if (!IsTrayAlive(trayPid))
            {
                _lifetime.StopApplication();
                return;
            }
        }
    }

    private static bool IsTrayAlive(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
