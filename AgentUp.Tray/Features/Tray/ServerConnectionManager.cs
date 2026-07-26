using System.Reactive.Subjects;

namespace AgentUp.Tray.Features.Tray;

public sealed class ServerConnectionManager : IDisposable
{
    private static readonly Uri DefaultServerUri = new("http://127.0.0.1:5000");
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly BehaviorSubject<ServiceState> _state = new(ServiceState.Connecting);
    private CancellationTokenSource _cts = new();

    public IObservable<ServiceState> State => _state;
    public ServiceState CurrentState => _state.Value;

    public ServerConnectionManager() : this(ResolveServerUri()) { }

    public ServerConnectionManager(Uri serverUri)
    {
        _http = new HttpClient { BaseAddress = serverUri, Timeout = HttpTimeout };
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        var token = _cts.Token;
        _ = PollLoopAsync(token);
        _ = HeartbeatLoopAsync(token);
        return Task.CompletedTask;
    }

    public async Task RestartAsync()
    {
        _state.OnNext(ServiceState.Restarting);
        try
        {
            await _http.PostAsync("/api/service/restart", null, _cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TaskCanceledException)
        {
            // Server closes the connection mid-shutdown; expected
        }
    }

    public async Task QuitAsync()
    {
        _cts.Cancel();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await _http.PostAsync("/api/service/shutdown", null, cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TaskCanceledException)
        {
            // Best-effort; server may already be unreachable
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            bool success;
            try
            {
                using var response = await _http.GetAsync("/api/workspaces", ct);
                success = response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                success = false;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }

            if (success)
                _state.OnNext(ServiceState.Connected);
            else if (_state.Value == ServiceState.Connected)
                _state.OnNext(ServiceState.Disconnected);

            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(HeartbeatInterval, ct); }
            catch (OperationCanceledException) { return; }

            if (_state.Value != ServiceState.Connected)
                continue;

            try
            {
                await _http.PostAsync("/api/tray/heartbeat", null, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                // Poll loop will detect and surface the disconnection
            }
        }
    }

    private static Uri ResolveServerUri()
    {
        var env = Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL");
        if (!string.IsNullOrWhiteSpace(env) && Uri.TryCreate(env, UriKind.Absolute, out var uri))
            return uri;
        return DefaultServerUri;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _http.Dispose();
        _state.Dispose();
    }
}
