using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserRemoteDisplayService(ILogger<BrowserRemoteDisplayService> logger)
{
    private static readonly TimeSpan PollingViewerTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ActiveInputTtl = TimeSpan.FromMilliseconds(1200);
    // The display loop broadcasts at 50-200ms cadence when subscribers are active. If the
    // cached frame is older than this, either the loop stalled (session died, workspace
    // restart mid-flight, subscribers gone) or Chromium's screenshot is slow. Either way,
    // the polling HTTP endpoint should force a fresh capture rather than serve stale bytes.
    private static readonly TimeSpan CachedFrameFreshness = TimeSpan.FromMilliseconds(400);
    private readonly ConcurrentDictionary<string, WorkspaceSubscriberSet> _subscribers = new();
    private readonly ConcurrentDictionary<string, byte[]> _latestFrames = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _latestFrameAt = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pollingViewers = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _activeInput = new();


    public bool HasSubscribers(string workspaceId)
        => (_subscribers.TryGetValue(workspaceId, out var subs) && !subs.IsEmpty)
           || HasRecentPollingViewer(workspaceId);

    public void RegisterPollingViewer(string workspaceId)
        => _pollingViewers[workspaceId] = DateTimeOffset.UtcNow.Add(PollingViewerTtl);

    public void RegisterInputActivity(string workspaceId)
        => _activeInput[workspaceId] = DateTimeOffset.UtcNow.Add(ActiveInputTtl);

    public bool HasActiveInput(string workspaceId)
    {
        if (!_activeInput.TryGetValue(workspaceId, out var expiresAt))
            return false;

        if (expiresAt > DateTimeOffset.UtcNow)
            return true;

        _activeInput.TryRemove(workspaceId, out _);
        return false;
    }

    public async Task ConnectAsync(string workspaceId, WebSocket ws, Func<string, Task>? onTextFrame, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var drainTask = DrainAndHandleInputAsync(ws, cts, workspaceId, onTextFrame);
        try
        {
            await SubscribeAsync(workspaceId, ws, cts.Token);
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("RDP display connection for workspace {WorkspaceId} ended.", SanitizeForLog(workspaceId));
        }
        finally
        {
            await cts.CancelAsync();
            try { await drainTask; }
            catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
            {
                logger.LogDebug(ex, "RDP input drain ended with error for workspace {WorkspaceId}.", SanitizeForLog(workspaceId));
            }
        }
    }

    public async Task BroadcastTextAsync(string workspaceId, string text, CancellationToken ct)
    {
        if (!_subscribers.TryGetValue(workspaceId, out var subs)) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        var segment = new ArraySegment<byte>(bytes);
        foreach (var ws in subs.Snapshot().Where(ws => ws.State == WebSocketState.Open))
        {
            try
            {
                await ws.SendAsync(segment, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
            catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
            {
                logger.LogDebug(ex, "RDP control frame send failed for workspace {WorkspaceId}.", SanitizeForLog(workspaceId));
            }
        }
    }

    public async Task BroadcastFrameAsync(string workspaceId, byte[] frame, CancellationToken ct)
    {
        _latestFrames[workspaceId] = frame.ToArray();
        _latestFrameAt[workspaceId] = DateTimeOffset.UtcNow;
        if (!_subscribers.TryGetValue(workspaceId, out var subs)) return;
        var segment = new ArraySegment<byte>(frame);
        foreach (var ws in subs.Snapshot().Where(ws => ws.State == WebSocketState.Open))
        {
            try
            {
                await ws.SendAsync(segment, WebSocketMessageType.Binary, endOfMessage: true, ct);
            }
            catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
            {
                logger.LogDebug(ex, "RDP bitmap frame send failed for workspace {WorkspaceId}.", SanitizeForLog(workspaceId));
            }
        }
    }

    public async Task DisconnectAllAsync(string workspaceId, CancellationToken ct)
    {
        // Frame cache invalidated: next subscriber won't receive a stale frame, and stream
        // state stays SessionLaunching until a genuinely new frame is broadcast.
        _latestFrames.TryRemove(workspaceId, out _);
        _latestFrameAt.TryRemove(workspaceId, out _);
        if (!_subscribers.TryGetValue(workspaceId, out var subs)) return;
        foreach (var ws in subs.Snapshot().Where(ws => ws.State == WebSocketState.Open))
        {
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct); }
            catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
            {
                logger.LogDebug(ex, "RDP disconnect failed for workspace {WorkspaceId}.", SanitizeForLog(workspaceId));
            }
        }
    }

    public bool TryGetLatestFrame(string workspaceId, out byte[] frame)
    {
        if (_latestFrames.TryGetValue(workspaceId, out var stored))
        {
            frame = stored.ToArray();
            return true;
        }

        frame = [];
        return false;
    }

    public async Task<byte[]?> GetLatestFrameOrCaptureAsync(
        string workspaceId,
        Func<CancellationToken, Task<byte[]?>> captureFrame,
        CancellationToken ct)
    {
        RegisterPollingViewer(workspaceId);
        // Fresh cached frame → serve it (the display loop is producing frames). Otherwise
        // fall through to a live capture, which broadcasts and refreshes the cache. Serving
        // an indefinitely-stale cached frame masks a stalled display loop and leaves the
        // desktop showing an old "connection refused" or workspace-restart error frame.
        if (TryGetLatestFrame(workspaceId, out var cached)
            && _latestFrameAt.TryGetValue(workspaceId, out var at)
            && DateTimeOffset.UtcNow - at < CachedFrameFreshness)
            return cached;
        return await captureFrame(ct);
    }

    private bool HasRecentPollingViewer(string workspaceId)
    {
        if (!_pollingViewers.TryGetValue(workspaceId, out var expiresAt))
            return false;

        if (expiresAt > DateTimeOffset.UtcNow)
            return true;

        _pollingViewers.TryRemove(workspaceId, out _);
        return false;
    }

    private async Task SubscribeAsync(string workspaceId, WebSocket ws, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var subs = _subscribers.GetOrAdd(workspaceId, _ => new WorkspaceSubscriberSet());
        subs.Add(id, ws);
        // Send the most recent cached frame immediately so the viewer isn't blank while
        // waiting for the display loop's next iteration (up to 200 ms on reconnect).
        if (_latestFrames.TryGetValue(workspaceId, out var snapshot) && ws.State == WebSocketState.Open)
        {
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(snapshot), WebSocketMessageType.Binary, endOfMessage: true, ct);
            }
            catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
            {
                logger.LogDebug(ex, "Failed to send initial frame to RDP subscriber for workspace {WorkspaceId}.", SanitizeForLog(workspaceId));
            }
        }
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        finally
        {
            subs.Remove(id);
            if (subs.IsEmpty)
                _subscribers.TryRemove(workspaceId, out _);
        }
    }

    private async Task DrainAndHandleInputAsync(
        WebSocket ws, CancellationTokenSource cts, string workspaceId, Func<string, Task>? onTextFrame)
    {
        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType != WebSocketMessageType.Text || onTextFrame is null) continue;
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try { await onTextFrame(json); }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { break; }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
        {
            logger.LogDebug(ex, "RDP WebSocket connection for workspace {WorkspaceId} closed unexpectedly.", SanitizeForLog(workspaceId));
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    private static string SanitizeForLog(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
             .Replace("\n", string.Empty, StringComparison.Ordinal);
}
