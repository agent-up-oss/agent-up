using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using Avalonia.Threading;

namespace AgentUp.Desktop.Features.Workspaces.Providers;

internal sealed class WorkspaceEventClient(HttpClient http, WorkspaceListViewModel sidebar) : IDisposable
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(150);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private CancellationTokenSource? _cts;
    private readonly Lock _refreshLock = new();
    private readonly Dictionary<string, CancellationTokenSource> _refreshes = new();

    public void Start()
    {
        Stop();
        CancellationToken token;
        lock (_refreshLock)
        {
            _cts = new CancellationTokenSource();
            token = _cts.Token;
        }

        _ = RunAsync(token);
    }

    public void Stop()
    {
        CancellationTokenSource? cts;

        lock (_refreshLock)
        {
            cts = _cts;
            _cts = null;

            foreach (var refresh in _refreshes.Values)
            {
                refresh.Cancel();
                refresh.Dispose();
            }

            _refreshes.Clear();
        }

        cts?.Cancel();
        cts?.Dispose();
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

            Dispatcher.UIThread.Post(() =>
            {
                if (!IsStarted())
                    return;

                sidebar.ApplyEvent(evt.WorkspaceId, evt.State, appChanges);
                ScheduleWorkspaceRefresh(evt.WorkspaceId);
            });
        }
    }

    private bool IsStarted()
    {
        lock (_refreshLock)
            return _cts is not null;
    }

    private void ScheduleWorkspaceRefresh(string workspaceId)
    {
        CancellationTokenSource refreshCts;
        lock (_refreshLock)
        {
            if (_cts is null)
                return;

            if (_refreshes.Remove(workspaceId, out var existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            refreshCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _refreshes[workspaceId] = refreshCts;
        }

        _ = RefreshWorkspaceAfterDelayAsync(workspaceId, refreshCts);
    }

    private async Task RefreshWorkspaceAfterDelayAsync(string workspaceId, CancellationTokenSource refreshCts)
    {
        try
        {
            await Task.Delay(RefreshDelay, refreshCts.Token);
            await Dispatcher.UIThread.InvokeAsync(
                async () => await sidebar.RefreshWorkspaceAsync(workspaceId, refreshCts.Token));
        }
        catch (OperationCanceledException) when (refreshCts.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            CompleteWorkspaceRefresh(workspaceId, refreshCts);
        }
    }

    private void CompleteWorkspaceRefresh(string workspaceId, CancellationTokenSource refreshCts)
    {
        lock (_refreshLock)
        {
            if (_refreshes.TryGetValue(workspaceId, out var current) && ReferenceEquals(current, refreshCts))
                _refreshes.Remove(workspaceId);
        }

        refreshCts.Dispose();
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
