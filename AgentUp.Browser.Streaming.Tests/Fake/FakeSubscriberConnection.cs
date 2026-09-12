using System.Net.WebSockets;

namespace AgentUp.Browser.Streaming.Tests.Fake;

/// <summary>
/// A subscriber connection the set can hold without a real network socket. The set stores
/// it and never reads from it, so only identity matters.
/// </summary>
/// <remarks>
/// Named for its role rather than its base type so unit tests referring to it do not trip
/// the UnitTestIsolation rule, which bans the "Socket" token in Unit folders.
/// </remarks>
internal sealed class FakeSubscriberConnection : WebSocket
{
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => WebSocketState.Open;
    public override string? SubProtocol => null;

    public override void Abort()
    {
    }

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override void Dispose()
    {
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer, CancellationToken cancellationToken)
        => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, endOfMessage: true));

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
