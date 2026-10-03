using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AgentUp.AUDebug.Tests.Fake;

/// <summary>
/// Chromium's debugger HTTP list plus a CDP websocket, so Mobile screens can open and drive
/// a page without launching a real browser.
/// </summary>
public sealed class FakeChromiumDebugEndpoint : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _shutdown = new();

    public FakeChromiumDebugEndpoint()
    {
        Port = FreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _ = Task.Run(ServeSafelyAsync);
    }

    public int Port { get; }

    public ConcurrentBag<string> Methods { get; } = [];

    public bool PointAvailable { get; set; } = true;

    public int MissingPointReplies { get; set; }

    public int JsonListFailures { get; set; }

    public static readonly byte[] Png = [137, 80, 78, 71];

    public void Dispose()
    {
        _shutdown.Cancel();
        _listener.Close();
        _shutdown.Dispose();
    }

    private async Task ServeSafelyAsync()
    {
        try
        {
            await ServeAsync();
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            System.Diagnostics.Trace.WriteLine(exception.Message);
        }
    }

    private async Task ServeAsync()
    {
        while (!_shutdown.IsCancellationRequested)
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

            _ = Task.Run(() => HandleContextAsync(context));
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context)
    {
        try
        {
            if (context.Request.IsWebSocketRequest)
            {
                await ServeWebSocketAsync(context);
                return;
            }

            await ServeHttpAsync(context);
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or OperationCanceledException or WebSocketException)
        {
            System.Diagnostics.Trace.WriteLine(exception.Message);
        }
    }

    private async Task ServeHttpAsync(HttpListenerContext context)
    {
        if (context.Request.Url?.AbsolutePath is "/json/list" or "/json")
        {
            if (JsonListFailures > 0)
            {
                JsonListFailures--;
                context.Response.StatusCode = 503;
                context.Response.Close();
                return;
            }

            var payload = JsonSerializer.Serialize(new[]
            {
                new { webSocketDebuggerUrl = $"ws://127.0.0.1:{Port}/devtools/page/1" }
            });
            var bytes = Encoding.UTF8.GetBytes(payload);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, _shutdown.Token);
            context.Response.Close();
            return;
        }

        context.Response.StatusCode = 404;
        context.Response.Close();
    }

    private async Task ServeWebSocketAsync(HttpListenerContext context)
    {
        var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
        var buffer = new byte[8192];
        while (socket.State == WebSocketState.Open && !_shutdown.IsCancellationRequested)
        {
            var received = await socket.ReceiveAsync(buffer, _shutdown.Token);
            if (received.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                return;
            }

            var request = Encoding.UTF8.GetString(buffer, 0, received.Count);
            using var document = JsonDocument.Parse(request);
            var id = document.RootElement.GetProperty("id").GetInt32();
            var method = document.RootElement.GetProperty("method").GetString() ?? "";
            Methods.Add(method);
            var reply = Reply(id, method);
            await socket.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, true, _shutdown.Token);
        }
    }

    private string Reply(int id, string method)
    {
        if (method == "Runtime.evaluate")
        {
            if (!PointAvailable || MissingPointReplies-- > 0)
            {
                return JsonSerializer.Serialize(new
                {
                    id,
                    result = new { result = new { type = "object", value = (object?)null } }
                });
            }

            return JsonSerializer.Serialize(new
            {
                id,
                result = new { result = new { type = "object", value = new { x = 12, y = 34 } } }
            });
        }

        if (method == "Page.captureScreenshot")
        {
            return JsonSerializer.Serialize(new
            {
                id,
                result = new { data = Convert.ToBase64String(Png) }
            });
        }

        return JsonSerializer.Serialize(new { id, result = new { } });
    }

    private static int FreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
