using System.Net.WebSockets;

namespace AgentUp.Browser.Streaming.Tests.Fake;

/// <summary>
/// Records outbound frames for display-service tests without a real network connection.
/// Named to keep the "Socket" token out of Unit folders.
/// </summary>
internal sealed class RecordingSubscriberConnection : WebSocket
{
    private WebSocketState _state = WebSocketState.Open;

    public List<(string Kind, byte[] Payload)> Messages { get; } = [];

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    public override void Abort() => _state = WebSocketState.Aborted;

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        CloseAsync(closeStatus, statusDescription, cancellationToken);

    public override void Dispose() => _state = WebSocketState.Closed;

    public override async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
    }

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        Messages.Add((messageType == WebSocketMessageType.Binary ? "binary" : "text", buffer.ToArray()));
        return Task.CompletedTask;
    }
}
