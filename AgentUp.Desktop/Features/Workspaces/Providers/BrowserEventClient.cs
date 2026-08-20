using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using AgentUp.Desktop.Features.Browser.Models;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

internal sealed class BrowserEventClient(HttpClient http) : IDisposable
{
    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<StreamStateSnapshot>? StreamStateChanged;

    private CancellationTokenSource? _cts;

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        cts?.Cancel();
        cts?.Dispose();
    }

    public void Dispose()
    {
        Stop();
        http.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            var wasConnected = false;
            try
            {
                wasConnected = await ConsumeAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
            {
                Trace.TraceWarning($"[BrowserEventClient] Disconnected: {ex.Message}");
            }

            if (wasConnected)
            {
                Disconnected?.Invoke();
                delay = TimeSpan.FromSeconds(1);
            }

            if (!await DelayAsync(delay, ct)) break;
            if (delay < TimeSpan.FromSeconds(30)) delay *= 2;
        }
    }

    private async Task<bool> ConsumeAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/browser/events");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        Connected?.Invoke();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) return true;

            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var json = line["data: ".Length..];
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp)) continue;
                if (typeProp.GetString() != "stream-state") continue;

                var snapshot = ParseStreamState(root);
                if (snapshot is not null) StreamStateChanged?.Invoke(snapshot);
            }
            catch (JsonException ex)
            {
                Trace.TraceWarning($"[BrowserEventClient] Malformed event JSON, skipping: {ex.Message}");
            }
        }

        return true;
    }

    private static StreamStateSnapshot? ParseStreamState(JsonElement root)
    {
        var wsId = root.TryGetProperty("workspaceId", out var w) ? w.GetString() ?? "" : "";
        var kindStr = root.TryGetProperty("kind", out var k) ? k.GetString() : null;
        if (kindStr is null) return null;

        var kind = kindStr switch
        {
            "chromium_downloading" => StreamStateKind.ChromiumDownloading,
            "workspace_stopped" => StreamStateKind.WorkspaceStopped,
            "app_connecting" => StreamStateKind.AppConnecting,
            "app_failed" => StreamStateKind.AppFailed,
            "session_launching" => StreamStateKind.SessionLaunching,
            "streaming" => StreamStateKind.Streaming,
            _ => (StreamStateKind?)null,
        };
        if (kind is null) return null;

        return new StreamStateSnapshot(
            WorkspaceId: wsId,
            Kind: kind.Value,
            ChromiumState: TryStr(root, "chromiumState"),
            ChromiumProgress: TryInt(root, "chromiumProgress"),
            Attempt: TryInt(root, "attempt"),
            MaxAttempts: TryInt(root, "maxAttempts"),
            Reason: TryStr(root, "reason"),
            CurrentUrl: TryStr(root, "currentUrl"));
    }

    private static string? TryStr(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int TryInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 0;

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
    }
}
