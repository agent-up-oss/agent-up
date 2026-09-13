#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AgentUp.Browser.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Browser.Streaming.Tests.Features.RemoteDisplay.Unit;

[TestFixture]
public sealed class BrowserRemoteDisplayServiceTests
{
    [Test]
    public async Task Connected_viewer_receives_control_text_and_binary_frames()
    {
        var service = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        using var socket = new RecordingWebSocket();
        using var cancellation = new CancellationTokenSource();
        var connection = service.ConnectAsync("workspace", socket, null, cancellation.Token);
        await WaitUntilAsync(() => service.HasSubscribers("workspace"));

        await service.BroadcastTextAsync("workspace", "control", CancellationToken.None);
        await service.BroadcastFrameAsync("workspace", [1, 2, 3], CancellationToken.None);
        await WaitUntilAsync(() => socket.Messages.Any(message => message.Type == WebSocketMessageType.Binary));

        Assert.Multiple(() =>
        {
            Assert.That(socket.Messages.Any(message => message.Type == WebSocketMessageType.Text), Is.True);
            Assert.That(socket.Messages.Single(message => message.Type == WebSocketMessageType.Binary).Payload,
                Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(service.TryGetLatestFrame("workspace", out var frame), Is.True);
            Assert.That(frame, Is.EqualTo(new byte[] { 1, 2, 3 }));
        });

        await cancellation.CancelAsync();
        await connection;
        Assert.That(service.HasSubscribers("workspace"), Is.False);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private sealed class RecordingWebSocket : WebSocket
    {
        private WebSocketState _state = WebSocketState.Open;
        public List<(WebSocketMessageType Type, byte[] Payload)> Messages { get; } = [];
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;
        public override void Abort() => _state = WebSocketState.Aborted;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            CloseAsync(closeStatus, statusDescription, cancellationToken);
        public override void Dispose() => _state = WebSocketState.Closed;
        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            Messages.Add((messageType, buffer.ToArray()));
            return Task.CompletedTask;
        }
    }
}
