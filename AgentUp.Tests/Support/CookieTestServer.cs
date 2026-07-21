using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace AgentUp.Tests.Support;

// Minimal HTTP server for cookie isolation tests.
// GET /set/{name}/{value}  → responds with Set-Cookie: name=value
// GET /check (or any other path) → body echoes the incoming Cookie header (or "(none)")
internal sealed class CookieTestServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly ConcurrentQueue<ReceivedRequest> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    internal sealed record ReceivedRequest(string Path, string? CookieHeader);

    public int Port { get; }

    public CookieTestServer()
    {
        Port = FindFreePort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        _listener.Start();
        _ = ListenAsync();
    }

    private static int FindFreePort()
    {
        using var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    private async Task ListenAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                _ = ex;
                break;
            }

            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            var cookieHeader = ctx.Request.Headers["Cookie"];

            string bodyText;
            if (path.StartsWith("/set/"))
            {
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    ctx.Response.AppendCookie(new Cookie(parts[1], parts[2]) { Path = "/" });
                    bodyText =
                        "<html><body>"
                        + $"{WebUtility.HtmlEncode(cookieHeader ?? "(none)")}"
                        + $"<script>document.cookie = '{parts[1]}={parts[2]}; path=/';</script>"
                        + "</body></html>";
                }
                else
                {
                    bodyText = $"<html><body>{WebUtility.HtmlEncode(cookieHeader ?? "(none)")}</body></html>";
                }
            }
            else
            {
                bodyText = $"<html><body>{WebUtility.HtmlEncode(cookieHeader ?? "(none)")}</body></html>";
            }

            var body = Encoding.UTF8.GetBytes(bodyText);
            ctx.Response.ContentType = "text/html";
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();

            _queue.Enqueue(new ReceivedRequest(path, cookieHeader));
            _signal.Release();
        }
    }

    // Waits until the server receives a request whose path starts with pathPrefix.
    // Non-matching requests (favicon.ico etc.) are discarded.
    public async Task<ReceivedRequest> WaitForRequestAsync(
        string pathPrefix, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(60));

        while (DateTime.UtcNow < deadline)
        {
            while (_queue.TryDequeue(out var req))
            {
                if (req.Path.StartsWith(pathPrefix, StringComparison.Ordinal))
                    return req;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;

            await _signal.WaitAsync(
                remaining < TimeSpan.FromMilliseconds(500)
                    ? remaining
                    : TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"No request to '{pathPrefix}' received within timeout");
    }

    public void Dispose()
    {
        try { _listener.Stop(); }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            _ = ex;
        }
    }
}
