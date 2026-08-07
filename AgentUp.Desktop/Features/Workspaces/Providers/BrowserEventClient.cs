using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

internal sealed class BrowserEventClient(HttpClient http) : IDisposable
{
    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<string, int>? ChromiumStatusChanged;

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

                if (typeProp.GetString() == "chromium-status")
                {
                    var state = root.TryGetProperty("state", out var sp) ? sp.GetString() ?? "not_started" : "not_started";
                    var progress = root.TryGetProperty("progress", out var pp) && pp.ValueKind == JsonValueKind.Number
                        ? pp.GetInt32() : 0;
                    ChromiumStatusChanged?.Invoke(state, progress);
                }
            }
            catch (JsonException ex)
            {
                Trace.TraceWarning($"[BrowserEventClient] Malformed event JSON, skipping: {ex.Message}");
            }
        }

        return true;
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
    }
}
