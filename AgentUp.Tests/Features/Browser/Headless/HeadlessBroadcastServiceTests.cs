using System.Net.WebSockets;
using AgentUp.Server;
using AgentUp.Server.Features.Browser.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Tests.Features.Browser.Headless;

// Verifies BrowserRemoteDisplayService frame routing through real in-process WebSocket connections.
// No Chromium is involved — frames are injected manually via BroadcastFrameAsync.
//
// Run: dotnet test AgentUp.Tests/ --filter "Category=Headless"
[TestFixture, Category("Headless")]
public sealed class HeadlessBroadcastServiceTests
{
    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _factory.DisposeAsync();
    }

    [Test, CancelAfter(10000)]
    public async Task Frame_is_received_by_connected_subscriber(CancellationToken ct)
    {
        using var ws = await ConnectAsync("ws-single", ct);

        var frame = new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0x03 };
        var display = _factory.Services.GetRequiredService<BrowserRemoteDisplayService>();

        await Task.Delay(50, ct);
        await display.BroadcastFrameAsync("ws-single", frame, ct);

        var received = await ReceiveFrameAsync(ws, ct);
        Assert.That(received, Is.EqualTo(frame));
    }

    [Test, CancelAfter(10000)]
    public async Task Frame_is_received_by_all_connected_subscribers(CancellationToken ct)
    {
        using var ws1 = await ConnectAsync("ws-multi", ct);
        using var ws2 = await ConnectAsync("ws-multi", ct);

        var frame = new byte[] { 0xFF, 0xD8, 0xAA, 0xBB };
        var display = _factory.Services.GetRequiredService<BrowserRemoteDisplayService>();

        await Task.Delay(50, ct);
        await display.BroadcastFrameAsync("ws-multi", frame, ct);

        var r1 = await ReceiveFrameAsync(ws1, ct);
        var r2 = await ReceiveFrameAsync(ws2, ct);

        Assert.Multiple(() =>
        {
            Assert.That(r1, Is.EqualTo(frame));
            Assert.That(r2, Is.EqualTo(frame));
        });
    }

    [Test, CancelAfter(10000)]
    public async Task Broadcast_to_disconnected_subscriber_does_not_throw(CancellationToken ct)
    {
        using var ws = await ConnectAsync("ws-closed", ct);

        await Task.Delay(50, ct);

        // Abort rather than CloseAsync: no two-way handshake, simulates abrupt disconnect.
        ws.Abort();
        await Task.Delay(100, ct);

        var display = _factory.Services.GetRequiredService<BrowserRemoteDisplayService>();

        Assert.DoesNotThrowAsync(async () =>
            await display.BroadcastFrameAsync("ws-closed", [0x01, 0x02], ct));
    }

    [Test, CancelAfter(10000)]
    public async Task Frame_is_not_routed_to_different_workspace(CancellationToken ct)
    {
        using var wsA = await ConnectAsync("ws-room-a", ct);
        using var wsB = await ConnectAsync("ws-room-b", ct);

        var frameA = new byte[] { 0xFF, 0xD8, 0xCA, 0xFE };
        var display = _factory.Services.GetRequiredService<BrowserRemoteDisplayService>();

        await Task.Delay(50, ct);
        await display.BroadcastFrameAsync("ws-room-a", frameA, ct);

        var received = await ReceiveFrameAsync(wsA, ct);
        Assert.That(received, Is.EqualTo(frameA), "ws-room-a subscriber should receive the frame");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var buffer = new byte[64];
        var receiveTask = wsB.ReceiveAsync(buffer, cts.Token);
        await Task.WhenAny(receiveTask, Task.Delay(400, CancellationToken.None));
        Assert.That(receiveTask.IsCompletedSuccessfully, Is.False,
            "ws-room-b should not have received any frame from ws-room-a");
        if (receiveTask.IsFaulted)
            await receiveTask; // re-throws so an unexpected server-side fault fails the test
    }

    private async Task<WebSocket> ConnectAsync(string workspaceId, CancellationToken ct)
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var uri = new Uri($"ws://localhost/api/browser/rdp/{workspaceId}");
        return await wsClient.ConnectAsync(uri, ct);
    }

    private static async Task<byte[]> ReceiveFrameAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[65536];
        var result = await ws.ReceiveAsync(buffer, ct);
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary));
        return buffer[..result.Count];
    }
}
