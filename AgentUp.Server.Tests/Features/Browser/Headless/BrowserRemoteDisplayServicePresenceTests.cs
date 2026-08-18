using AgentUp.Browser.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Browser.Headless;

[TestFixture]
public sealed class BrowserRemoteDisplayServicePresenceTests
{
    [Test]
    public void HasForegroundSubscribers_is_false_when_only_polling_viewer_absent_and_no_ws()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        Assert.That(display.HasForegroundSubscribers("workspace"), Is.False);
    }

    [Test]
    public void HasForegroundSubscribers_true_when_polling_viewer_registered()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        display.RegisterPollingViewer("workspace");
        Assert.That(display.HasForegroundSubscribers("workspace"), Is.True);
    }

    [Test]
    public void SetSubscriberPresence_returns_false_for_unknown_subscriber()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var ok = display.SetSubscriberPresence("workspace", Guid.NewGuid(), PresenceState.Background);
        Assert.That(ok, Is.False);
    }

    [Test]
    public void WorkspaceSubscriberSet_tracks_presence_per_subscriber_and_defaults_to_foreground()
    {
        var set = new WorkspaceSubscriberSet();
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        set.Add(idA, new FakeWebSocket());
        set.Add(idB, new FakeWebSocket());

        Assert.That(set.HasForeground(), Is.True);

        set.SetPresence(idA, PresenceState.Background);
        set.SetPresence(idB, PresenceState.Background);
        Assert.That(set.HasForeground(), Is.False);

        set.SetPresence(idA, PresenceState.Foreground);
        Assert.That(set.HasForeground(), Is.True);
    }

    [Test]
    public void SetPresence_returns_true_only_when_state_actually_changes()
    {
        var set = new WorkspaceSubscriberSet();
        var id = Guid.NewGuid();
        set.Add(id, new FakeWebSocket());

        Assert.That(set.SetPresence(id, PresenceState.Foreground), Is.False, "no-change should return false");
        Assert.That(set.SetPresence(id, PresenceState.Background), Is.True);
        Assert.That(set.SetPresence(id, PresenceState.Background), Is.False, "no-change should return false");
    }

    private sealed class FakeWebSocket : System.Net.WebSockets.WebSocket
    {
        public override System.Net.WebSockets.WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override System.Net.WebSockets.WebSocketState State => System.Net.WebSockets.WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(System.Net.WebSockets.WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;
        public override Task CloseOutputAsync(System.Net.WebSockets.WebSocketCloseStatus s, string? d, CancellationToken ct) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<System.Net.WebSockets.WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> b, CancellationToken ct) =>
            Task.FromResult(new System.Net.WebSockets.WebSocketReceiveResult(0, System.Net.WebSockets.WebSocketMessageType.Close, true));
        public override Task SendAsync(ArraySegment<byte> b, System.Net.WebSockets.WebSocketMessageType t, bool e, CancellationToken ct) => Task.CompletedTask;
    }
}
