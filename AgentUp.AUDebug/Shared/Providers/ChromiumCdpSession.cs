using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AgentUp.AUDebug.Shared.Providers;

/// <summary>
/// A request/response CDP connection to one Chromium page.
/// </summary>
/// <remarks>
/// A long route sends many commands down one socket while Chromium also pushes events down
/// it, so replies have to be matched by id. Reading the next frame and assuming it is the
/// answer works for a single command and silently reads an event for the second.
/// </remarks>
public sealed class ChromiumCdpSession : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private int _nextId;

    public static async Task<ChromiumCdpSession> ConnectAsync(string websocketUrl, CancellationToken cancellationToken)
    {
        var session = new ChromiumCdpSession();
        await session._socket.ConnectAsync(new Uri(websocketUrl), cancellationToken);
        return session;
    }

    public int NextId() => Interlocked.Increment(ref _nextId);

    public async Task<string> SendAsync(Func<int, string> message, CancellationToken cancellationToken)
    {
        var id = NextId();
        await _socket.SendAsync(Encoding.UTF8.GetBytes(message(id)), WebSocketMessageType.Text, true, cancellationToken);
        while (true)
        {
            var frame = await ReceiveAsync(cancellationToken);
            using var document = JsonDocument.Parse(frame);
            if (document.RootElement.TryGetProperty("id", out var replyId) && replyId.GetInt32() == id)
                return frame;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State == WebSocketState.Open)
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        _socket.Dispose();
    }

    private async Task<string> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        using var memory = new MemoryStream();
        while (true)
        {
            var result = await _socket.ReceiveAsync(buffer, cancellationToken);
            memory.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                return Encoding.UTF8.GetString(memory.ToArray());
        }
    }
}
