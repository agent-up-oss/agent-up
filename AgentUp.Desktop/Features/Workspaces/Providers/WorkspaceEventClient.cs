using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using Avalonia.Threading;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

internal sealed class WorkspaceEventClient(HttpClient http, WorkspaceListViewModel sidebar) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private CancellationTokenSource? _cts;

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(ct);
                if (!await DelayAsync(delay, ct)) break;
                if (delay < TimeSpan.FromSeconds(30)) delay *= 2;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
            {
                Trace.TraceWarning($"[WorkspaceEventClient] Disconnected: {ex.Message}");
                if (!await DelayAsync(delay, ct)) break;
                if (delay < TimeSpan.FromSeconds(30)) delay *= 2;
            }
        }
    }

    public void Dispose()
    {
        Stop();
        http.Dispose();
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspaces/events");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) return; // Server closed the connection.

            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var json = line["data: ".Length..];
            if (string.IsNullOrWhiteSpace(json)) continue;

            var evt = JsonSerializer.Deserialize<WorkspaceStateChangedEventDto>(json, JsonOptions);
            if (evt is null) continue;

            var appChanges = evt.Applications
                .Select(a => (a.Name, a.State))
                .ToList();

            Dispatcher.UIThread.Post(() => sidebar.ApplyEvent(evt.WorkspaceId, evt.State, appChanges));
        }
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
