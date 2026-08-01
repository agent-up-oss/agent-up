using System.Collections.Concurrent;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class ScreencastBroadcastService(ILogger<ScreencastBroadcastService> logger)
{
    private readonly ConcurrentDictionary<string, WorkspaceSubscriberSet> _subscribers = new();

    public async Task ConnectAsync(string workspaceId, WebSocket ws, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = DrainToDetectCloseAsync(ws, cts, workspaceId);
        await SubscribeAsync(workspaceId, ws, cts.Token);
        if (ws.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
    }

    public async Task BroadcastFrameAsync(string workspaceId, byte[] frame, CancellationToken ct)
    {
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
                logger.LogDebug(ex, "Screencast frame send failed for workspace {WorkspaceId}.", workspaceId);
            }
        }
    }

    private async Task SubscribeAsync(string workspaceId, WebSocket ws, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var subs = _subscribers.GetOrAdd(workspaceId, _ => new WorkspaceSubscriberSet());
        subs.Add(id, ws);
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        finally
        {
            subs.Remove(id);
        }
    }

    private async Task DrainToDetectCloseAsync(WebSocket ws, CancellationTokenSource cts, string workspaceId)
    {
        var buffer = new byte[64];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
        {
            logger.LogDebug(ex, "WebSocket connection for workspace {WorkspaceId} closed unexpectedly.", workspaceId);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }
}
