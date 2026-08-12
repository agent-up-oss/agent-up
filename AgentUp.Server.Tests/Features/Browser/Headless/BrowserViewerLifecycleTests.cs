using System.Net.WebSockets;
using System.Text;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Browser.Headless;

[TestFixture]
public sealed class BrowserViewerLifecycleTests
{
    private static BrowserRemoteDisplayService Build()
        => new(NullLogger<BrowserRemoteDisplayService>.Instance);

    // ── Frame cache ───────────────────────────────────────────────────────────

    [Test]
    public async Task FrameCache_isCleared_afterDisconnectAll()
    {
        var svc = Build();
        await svc.BroadcastFrameAsync("ws", [1, 2, 3], CancellationToken.None);
        Assert.That(svc.TryGetLatestFrame("ws", out _), Is.True, "precondition: frame should be cached");

        await svc.DisconnectAllAsync("ws", CancellationToken.None);

        Assert.That(svc.TryGetLatestFrame("ws", out _), Is.False);
    }

    [Test]
    public async Task FrameCache_isAvailable_afterRestartBroadcast()
    {
        var svc = Build();
        var firstFrame = new byte[] { 0xAA };
        var secondFrame = new byte[] { 0xBB };

        await svc.BroadcastFrameAsync("ws", firstFrame, CancellationToken.None);
        await svc.DisconnectAllAsync("ws", CancellationToken.None);
        await svc.BroadcastFrameAsync("ws", secondFrame, CancellationToken.None);

        Assert.That(svc.TryGetLatestFrame("ws", out var frame), Is.True);
        Assert.That(frame, Is.EqualTo(secondFrame));
    }

    [Test]
    public async Task RapidStopAndStart_doesNotServeStaleFrameFromPreviousLifecycle()
    {
        var svc = Build();
        var staleFrame = new byte[] { 0xFF };

        await svc.BroadcastFrameAsync("ws", staleFrame, CancellationToken.None);
        await svc.DisconnectAllAsync("ws", CancellationToken.None);

        // New lifecycle started but no fresh frame has arrived yet.
        Assert.That(svc.TryGetLatestFrame("ws", out _), Is.False, "stale frame must not survive disconnect");
    }

    [Test]
    public async Task SecondWorkspace_frameCacheIsIndependent()
    {
        var svc = Build();
        await svc.BroadcastFrameAsync("ws-A", [0xAA], CancellationToken.None);
        await svc.BroadcastFrameAsync("ws-B", [0xBB], CancellationToken.None);

        await svc.DisconnectAllAsync("ws-A", CancellationToken.None);

        Assert.That(svc.TryGetLatestFrame("ws-A", out _), Is.False, "ws-A cache cleared");
        Assert.That(svc.TryGetLatestFrame("ws-B", out _), Is.True, "ws-B cache untouched");
    }

    // ── Subscriber paused frame ───────────────────────────────────────────────

    [Test]
    public async Task Subscriber_receives_paused_control_frame_before_close()
    {
        var svc = Build();
        var socket = new CapturingFakeWebSocket();

        var connectTask = svc.ConnectAsync("ws", socket, onTextFrame: null, CancellationToken.None);
        await Task.Delay(30);  // let subscriber register and receive "active" frame

        await svc.DisconnectAllAsync("ws", CancellationToken.None);
        await connectTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(socket.TextMessages, Does.Contain("{\"type\":\"stream\",\"state\":\"paused\"}"));
    }

    [Test]
    public async Task Subscriber_receives_active_control_frame_on_connect()
    {
        var svc = Build();
        var socket = new CapturingFakeWebSocket();

        var connectTask = svc.ConnectAsync("ws", socket, onTextFrame: null, CancellationToken.None);
        await Task.Delay(30);  // let subscriber register

        await svc.DisconnectAllAsync("ws", CancellationToken.None);
        await connectTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(socket.TextMessages, Does.Contain("{\"type\":\"stream\",\"state\":\"active\"}"));
    }

    [Test]
    public async Task Subscriber_receives_active_before_paused()
    {
        var svc = Build();
        var socket = new CapturingFakeWebSocket();

        var connectTask = svc.ConnectAsync("ws", socket, onTextFrame: null, CancellationToken.None);
        await Task.Delay(30);

        await svc.DisconnectAllAsync("ws", CancellationToken.None);
        await connectTask.WaitAsync(TimeSpan.FromSeconds(2));

        var activeIdx = socket.TextMessages.IndexOf("{\"type\":\"stream\",\"state\":\"active\"}");
        var pausedIdx = socket.TextMessages.IndexOf("{\"type\":\"stream\",\"state\":\"paused\"}");
        Assert.That(activeIdx, Is.GreaterThanOrEqualTo(0), "active frame must be sent");
        Assert.That(pausedIdx, Is.GreaterThanOrEqualTo(0), "paused frame must be sent");
        Assert.That(activeIdx, Is.LessThan(pausedIdx), "active must precede paused");
    }

    // ── Fake WebSocket ────────────────────────────────────────────────────────

    private sealed class CapturingFakeWebSocket : WebSocket
    {
        private WebSocketState _state = WebSocketState.Open;
        private readonly TaskCompletionSource _closedTcs = new();

        public List<string> TextMessages { get; } = [];

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;
        public override void Abort() { }

        public override Task CloseAsync(WebSocketCloseStatus s, string? d, CancellationToken ct)
        {
            _state = WebSocketState.Closed;
            _closedTcs.TrySetResult();
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus s, string? d, CancellationToken ct)
            => CloseAsync(s, d, ct);

        public override void Dispose() { }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> b, CancellationToken ct)
        {
            await _closedTcs.Task.WaitAsync(ct);
            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
        }

        public override Task SendAsync(ArraySegment<byte> b, WebSocketMessageType t, bool e, CancellationToken ct)
        {
            if (t == WebSocketMessageType.Text)
                TextMessages.Add(Encoding.UTF8.GetString(b));
            return Task.CompletedTask;
        }
    }
}
