using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace AgentUp.Tests.Support;

// A minimal local web application for Desktop browser end-to-end tests: it serves one page on
// every GET and accepts raw file uploads on POST /upload?name=..., which is how the test page
// ships a picked file back over real HTTP. Each response echoes the request's nav query value
// into window.__nav so tests can tell one loaded document from the next.
internal sealed class HtmlAppServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _html;
    private readonly ConcurrentQueue<ReceivedUpload> _uploads = new();
    private readonly SemaphoreSlim _uploadSignal = new(0);

    // Test pages assign this placeholder to window.__nav; the server replaces it per request.
    internal const string NavigationTokenPlaceholder = "__AGENTUP_NAV__";

    internal sealed record ReceivedUpload(string Name, byte[] Content, string? ContentType);

    internal int Port { get; }

    internal string BaseUrl => $"http://localhost:{Port}/";

    internal HtmlAppServer(string html)
    {
        _html = html;
        Port = LoopbackPorts.FindFree();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        _listener.Start();
        _ = AcceptAsync();
    }

    private async Task AcceptAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            await RespondAsync(context);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException)
        {
            Trace.TraceWarning($"[HtmlAppServer] request failed: {ex.Message}");
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        if (path == "/upload")
        {
            await RecordUploadAsync(context);
            return;
        }

        var page = _html.Replace(
            NavigationTokenPlaceholder,
            context.Request.QueryString["nav"] ?? string.Empty,
            StringComparison.Ordinal);
        var body = Encoding.UTF8.GetBytes(page);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    private async Task RecordUploadAsync(HttpListenerContext context)
    {
        using var buffer = new MemoryStream();
        await context.Request.InputStream.CopyToAsync(buffer);
        var name = context.Request.QueryString["name"] ?? string.Empty;
        _uploads.Enqueue(new ReceivedUpload(name, buffer.ToArray(), context.Request.ContentType));
        _uploadSignal.Release();

        context.Response.StatusCode = 204;
        context.Response.Close();
    }

    internal async Task<ReceivedUpload> WaitForUploadAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_uploads.TryDequeue(out var upload))
                return upload;

            await _uploadSignal.WaitAsync(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException("The Desktop WebView never uploaded a picked file to /upload.");
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            Trace.TraceWarning(ex.Message);
        }

        _uploadSignal.Dispose();
    }
}
