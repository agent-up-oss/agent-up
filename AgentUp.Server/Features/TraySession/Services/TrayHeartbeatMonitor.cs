using Microsoft.Extensions.Hosting;
using AgentUp.Server.Features.ServiceControl.Interfaces;

namespace AgentUp.Server.Features.TraySession.Services;

// Shuts down the server if a registered tray stops sending heartbeats.
// Only activates after the first heartbeat is received; headless deployments (no tray) are never affected.
public sealed class TrayHeartbeatMonitor : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IProcessExitCode _exitCode;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _checkInterval;
    private DateTimeOffset? _lastHeartbeat;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DefaultCheckInterval = TimeSpan.FromSeconds(5);

    public TrayHeartbeatMonitor(IHostApplicationLifetime lifetime, IProcessExitCode exitCode)
        : this(lifetime, exitCode, DefaultTimeout, DefaultCheckInterval) { }

    internal TrayHeartbeatMonitor(IHostApplicationLifetime lifetime, IProcessExitCode exitCode, TimeSpan timeout, TimeSpan checkInterval)
    {
        _lifetime = lifetime;
        _exitCode = exitCode;
        _timeout = timeout;
        _checkInterval = checkInterval;
    }

    public void RegisterHeartbeat()
    {
        lock (_lock) _lastHeartbeat = DateTimeOffset.UtcNow;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = MonitorAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task MonitorAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(_checkInterval, ct); }
            catch (OperationCanceledException) { return; }

            DateTimeOffset? lastHeartbeat;
            lock (_lock) lastHeartbeat = _lastHeartbeat;

            if (lastHeartbeat is null)
                continue;

            if (DateTimeOffset.UtcNow - lastHeartbeat.Value > _timeout)
            {
                // Exit with non-zero so systemd/launchd restarts the server after a stale tray disconnect.
                _exitCode.Set(1);
                _lifetime.StopApplication();
                return;
            }
        }
    }
}
