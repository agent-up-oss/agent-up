using System.Reactive.Subjects;

namespace AgentUp.Tray.Features.Tray;

public sealed class ServerConnectionManager : IDisposable
{
    private static readonly Uri DefaultServerUri = new("http://127.0.0.1:5000");
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly BehaviorSubject<ServiceState> _state = new(ServiceState.Connecting);
    private readonly CancellationTokenSource _cts = new();

    public IObservable<ServiceState> State => _state;
    public ServiceState CurrentState => _state.Value;

    public ServerConnectionManager()
        : this(ResolveServerUri(Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL")))
    {
    }

    public ServerConnectionManager(Uri serverUri)
        : this(new HttpClient { BaseAddress = serverUri, Timeout = HttpTimeout })
    {
    }

    /// <summary>
    /// Takes the client so the poll, restart and shutdown requests can be observed without
    /// a real server.
    /// </summary>
    public ServerConnectionManager(HttpClient http)
    {
        _http = http;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        var token = ct.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct).Token
            : _cts.Token;
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

            if (ServiceStateTransition.Next(_state.Value, success) is { } next)
                _state.OnNext(next);

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

            if (!ServiceStateTransition.ShouldHeartbeat(_state.Value))
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

    /// <summary>
    /// Resolves the server URI from a configured value, falling back to the local default.
    /// Takes the raw value rather than reading the environment, so the fallback rules are
    /// testable without mutating process state.
    /// </summary>
    /// <remarks>
    /// The scheme has to be checked, not just absoluteness: Uri.TryCreate accepts
    /// "localhost:5000" as absolute with "localhost" as the scheme, so a value like that
    /// would otherwise be used verbatim and leave the tray permanently disconnected
    /// instead of falling back.
    /// </remarks>
    public static Uri ResolveServerUri(string? configuredUrl)
        => !string.IsNullOrWhiteSpace(configuredUrl)
           && Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
           && uri.Scheme is "http" or "https"
            ? uri
            : DefaultServerUri;

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _http.Dispose();
        _state.Dispose();
    }
}
