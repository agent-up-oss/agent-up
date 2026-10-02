using System.Net;
using System.Net.WebSockets;
using System.Text;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

/// <summary>
/// The session exists to match replies by id, so it is tested against a socket that answers
/// out of order and pushes events in between - which is what a real page does.
/// </summary>
[TestFixture]
public sealed class ChromiumCdpSessionTests
{
    [Test]
    public async Task Send_returnsTheReplyToItsOwnCommand()
    {
        using var server = new FakeCdpServer();
        await using var session = await ChromiumCdpSession.ConnectAsync(server.Url, CancellationToken.None);

        var reply = await session.SendAsync(id => Command(id, "first"), CancellationToken.None);

        Assert.That(reply, Does.Contain("\"echo\":\"first\""));
    }

    [Test]
    public async Task Send_skipsTheEventsChromiumPushesInBetween()
    {
        using var server = new FakeCdpServer { EventsBeforeEachReply = 3 };
        await using var session = await ChromiumCdpSession.ConnectAsync(server.Url, CancellationToken.None);

        var reply = await session.SendAsync(id => Command(id, "after-events"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(reply, Does.Contain("\"echo\":\"after-events\""));
            Assert.That(reply, Does.Not.Contain("Network.requestWillBeSent"));
        });
    }

    [Test]
    public async Task Send_matchesEachReplyToItsOwnCommandAcrossALongRoute()
    {
        using var server = new FakeCdpServer { EventsBeforeEachReply = 1 };
        await using var session = await ChromiumCdpSession.ConnectAsync(server.Url, CancellationToken.None);

        var first = await session.SendAsync(id => Command(id, "one"), CancellationToken.None);
        var second = await session.SendAsync(id => Command(id, "two"), CancellationToken.None);
        var third = await session.SendAsync(id => Command(id, "three"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first, Does.Contain("\"echo\":\"one\""));
            Assert.That(second, Does.Contain("\"echo\":\"two\""));
            Assert.That(third, Does.Contain("\"echo\":\"three\""));
        });
    }

    [Test]
    public async Task NextId_neverReusesAnIdWithinOneSession()
    {
        using var server = new FakeCdpServer();
        await using var session = await ChromiumCdpSession.ConnectAsync(server.Url, CancellationToken.None);

        var ids = Enumerable.Range(0, 5).Select(_ => session.NextId()).ToArray();

        Assert.That(ids, Is.Unique);
    }

    private static string Command(int id, string echo)
        => $"{{\"id\":{id},\"method\":\"Runtime.evaluate\",\"echo\":\"{echo}\"}}";

    /// <summary>A websocket that answers each command by id, optionally pushing events first.</summary>
    private sealed class FakeCdpServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _shutdown = new();

        public FakeCdpServer()
        {
            var port = FreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            Url = $"ws://127.0.0.1:{port}/";
            _ = Task.Run(ServeSafelyAsync);
        }

        public string Url { get; }

        public int EventsBeforeEachReply { get; init; }

        public void Dispose()
        {
            _shutdown.Cancel();
            _listener.Close();
            _shutdown.Dispose();
        }

        private async Task ServeAsync()
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            var buffer = new byte[8192];
            while (socket.State == WebSocketState.Open && !_shutdown.IsCancellationRequested)
            {
                var received = await socket.ReceiveAsync(buffer, _shutdown.Token);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    // Answer the close frame. ClientWebSocket.CloseAsync waits for this, so a
                    // server that just stops reading hangs every disposal.
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    return;
                }

                var request = Encoding.UTF8.GetString(buffer, 0, received.Count);
                for (var index = 0; index < EventsBeforeEachReply; index++)
                    await SendAsync(socket, "{\"method\":\"Network.requestWillBeSent\",\"params\":{}}");
                await SendAsync(socket, request.Replace("\"method\"", "\"result\":{},\"method\"", StringComparison.Ordinal));
            }
        }

        private async Task ServeSafelyAsync()
        {
            try
            {
                await ServeAsync();
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or HttpListenerException or ObjectDisposedException)
            {
                System.Diagnostics.Trace.WriteLine(exception.Message);
            }
        }

        private async Task SendAsync(WebSocket socket, string text)
            => await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, _shutdown.Token);

        private static int FreePort()
        {
            using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }
    }
}
