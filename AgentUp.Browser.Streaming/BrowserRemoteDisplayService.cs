using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AgentUp.Browser.Streaming;

public sealed class BrowserRemoteDisplayService(ILogger<BrowserRemoteDisplayService> logger)
{
    private static readonly TimeSpan PollingViewerTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ActiveInputTtl = TimeSpan.FromMilliseconds(1200);
    // The display loop broadcasts at 50-200ms cadence when subscribers are active. If the
    // cached frame is older than this, either the loop stalled (session died, workspace
    // restart mid-flight, subscribers gone) or Chromium's screenshot is slow. Either way,
    // the polling HTTP endpoint should force a fresh capture rather than serve stale bytes.
    private static readonly TimeSpan CachedFrameFreshness = TimeSpan.FromMilliseconds(400);
    // Per-subscriber cap for background viewers (unfocused window, non-active tab). Every
    // background subscriber gets at most this rate regardless of what other subscribers do.
    public static readonly TimeSpan BackgroundSubscriberInterval = TimeSpan.FromMilliseconds(1000);
    private readonly ConcurrentDictionary<string, WorkspaceSubscriberSet> _subscribers = new();
    private readonly ConcurrentDictionary<string, byte[]> _latestFrames = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _latestFrameAt = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pollingViewers = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _activeInput = new();


    public bool HasSubscribers(string workspaceId)
        => (_subscribers.TryGetValue(workspaceId, out var subs) && !subs.IsEmpty)
           || HasRecentPollingViewer(workspaceId);

    // Polling viewers can't send presence messages, so they count as foreground —
    // they're driven by an HTTP fetch loop that only runs when the client wants frames.
    public bool HasForegroundSubscribers(string workspaceId)
        => (_subscribers.TryGetValue(workspaceId, out var subs) && subs.HasForeground())
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
        var subscriberId = Guid.NewGuid();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var drainTask = DrainAndHandleInputAsync(ws, cts, workspaceId, subscriberId, onTextFrame);
        try
        {
            await SubscribeAsync(workspaceId, ws, subscriberId, cts.Token);
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
        foreach (var entry in subs.Snapshot().Where(e => e.Socket.State == WebSocketState.Open))
        {
            try
            {
                await entry.Socket.SendAsync(segment, WebSocketMessageType.Text, endOfMessage: true, ct);
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
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        var backgroundIntervalTicks = BackgroundSubscriberInterval.Ticks;
        // Filter deliverable subscribers in one LINQ pass: WS must be open, AND either
        // the subscriber is foreground OR its 1 fps window has elapsed. Background
        // subscribers are capped independently of the capture cadence a foreground peer
        // is driving.
        var deliverable = subs.Snapshot().Where(e =>
            e.Socket.State == WebSocketState.Open
            && (e.Presence != PresenceState.Background
                || nowTicks - e.LastFrameSentAtTicks >= backgroundIntervalTicks));
        foreach (var entry in deliverable)
        {
            try
            {
                await entry.Socket.SendAsync(segment, WebSocketMessageType.Binary, endOfMessage: true, ct);
                entry.LastFrameSentAtTicks = nowTicks;
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
        foreach (var entry in subs.Snapshot().Where(e => e.Socket.State == WebSocketState.Open))
        {
            // Tell the JS state machine we're pausing intentionally before we close, so
            // it can distinguish "server said stop" from "network dropped".
            await SendStreamStateAsync(entry.Socket, "paused", ct);
            try { await entry.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct); }
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

    public bool SetSubscriberPresence(string workspaceId, Guid subscriberId, PresenceState state)
        => _subscribers.TryGetValue(workspaceId, out var subs)
           && subs.SetPresence(subscriberId, state);

    private bool HasRecentPollingViewer(string workspaceId)
    {
        if (!_pollingViewers.TryGetValue(workspaceId, out var expiresAt))
            return false;

        if (expiresAt > DateTimeOffset.UtcNow)
            return true;

        _pollingViewers.TryRemove(workspaceId, out _);
        return false;
    }

    private async Task SubscribeAsync(string workspaceId, WebSocket ws, Guid subscriberId, CancellationToken ct)
    {
        var subs = _subscribers.GetOrAdd(workspaceId, _ => new WorkspaceSubscriberSet());
        subs.Add(subscriberId, ws);
        // Give the JS state machine an explicit "server is streaming to you now" signal
        // BEFORE the first frame. Protocol 1 message. JS uses it as an observable hint
        // (serverReportedActive); combined with the arrival of the first binary frame
        // it drives the open_no_frames → streaming transition deterministically.
        await SendStreamStateAsync(ws, "active", ct);
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
            subs.Remove(subscriberId);
            if (subs.IsEmpty)
                _subscribers.TryRemove(workspaceId, out _);
        }
    }

    // Server → JS Protocol 1 message. Best-effort — the WS may already be closing when
    // this fires (session teardown races with viewer disconnect), so exceptions are
    // swallowed at debug level.
    private async Task SendStreamStateAsync(WebSocket ws, string state, CancellationToken ct)
    {
        if (ws.State != WebSocketState.Open) return;
        var payload = Encoding.UTF8.GetBytes($$"""{"type":"stream","state":"{{state}}"}""");
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
        {
            logger.LogDebug(ex, "Failed to send stream-state control frame.");
        }
    }

    private async Task DrainAndHandleInputAsync(
        WebSocket ws, CancellationTokenSource cts, string workspaceId, Guid subscriberId, Func<string, Task>? onTextFrame)
    {
        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType != WebSocketMessageType.Text) continue;
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                // Presence messages are consumed here — never forwarded to the input handler,
                // which only cares about pointer/keyboard events.
                if (TryHandlePresenceMessage(workspaceId, subscriberId, json)) continue;
                if (onTextFrame is null) continue;
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

    private bool TryHandlePresenceMessage(string workspaceId, Guid subscriberId, string json)
    {
        // Cheap prefix check to skip the JSON parser for the common pointer/keyboard case.
        if (json.IndexOf("\"presence\"", StringComparison.Ordinal) < 0) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!root.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) return false;
            if (!string.Equals(typeEl.GetString(), "presence", StringComparison.Ordinal)) return false;
            if (!root.TryGetProperty("state", out var stateEl) || stateEl.ValueKind != JsonValueKind.String) return true;
            var state = stateEl.GetString();
            var presence = state switch
            {
                "foreground" => (PresenceState?)PresenceState.Foreground,
                "background" => (PresenceState?)PresenceState.Background,
                _ => null,
            };
            if (presence is null) return true;
            if (SetSubscriberPresence(workspaceId, subscriberId, presence.Value))
                logger.LogDebug(
                    "RDP subscriber {SubscriberId} presence → {State} for workspace {WorkspaceId}.",
                    subscriberId, presence.Value, SanitizeForLog(workspaceId));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string SanitizeForLog(string value) =>
        value.Replace("\r", string.Empty, StringComparison.Ordinal)
             .Replace("\n", string.Empty, StringComparison.Ordinal);
}
