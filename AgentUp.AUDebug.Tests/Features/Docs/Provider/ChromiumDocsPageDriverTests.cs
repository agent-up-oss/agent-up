using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AgentUp.AUDebug.Features.Docs.Providers;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Docs.Provider;

[TestFixture]
[NonParallelizable]
public sealed class ChromiumDocsPageDriverTests
{
    [Test]
    public void Capture_whenCanceled_killsBrowser()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumDocsPageDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}",
                Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png"),
                "What it is",
                true,
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started, Has.Count.EqualTo(1));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
        Assert.That(processes.Started[0].FileName, Is.EqualTo("chromium"));
        Assert.That(processes.Started[0].Arguments, Does.Contain($"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}"));
        Assert.That(processes.Started[0].Arguments, Does.Contain($"--remote-debugging-port={ChromiumDocsPageDriver.DebuggingPort}"));
        Assert.That(processes.Started[0].Arguments, Does.Contain($"--window-size={DebugLayout.DocsViewportWidth},{DebugLayout.DocsViewportHeight}"));
    }

    [Test]
    public void Capture_fallsBackToNixShellWhenChromiumIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var driver = new ChromiumDocsPageDriver(processes, new FakeEnvironment(), new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}/developer-guide/git",
                Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png"),
                null,
                false,
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("nix-shell"));
        Assert.That(processes.Started[0].Arguments, Does.Contain("chromium"));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
    }

    [Test]
    public void Capture_fallsBackToChromiumBrowserThenChrome()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium-browser"] = "/usr/bin/chromium-browser";
        var driver = new ChromiumDocsPageDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}/docs/",
                Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png"),
                null,
                false,
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("chromium-browser"));

        processes.Started.Clear();
        environment.Executables.Clear();
        environment.Executables["google-chrome"] = "/usr/bin/google-chrome";
        Assert.That(
            async () => await driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}/docs/",
                Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png"),
                null,
                false,
                timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("google-chrome"));
    }

    [Test]
    public async Task Capture_drivesCdpForAHeadingAndFullPageScreenshot()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var output = Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "docs.png");
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumDocsPageDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{ChromiumDocsPageDriver.DebuggingPort}/");
        var accept = Task.CompletedTask;
        try
        {
            var capture = driver.CaptureAsync(
                $"{DebugLayout.DocsUrl}{DebugLayout.DocsHomePath}",
                output,
                "What it is",
                true,
                timeout.Token);
            await Task.Delay(250, timeout.Token);
            listener.Start();
            accept = AcceptCdpAsync(listener, timeout.Token);
            await capture;
        }
        finally
        {
            if (listener.IsListening)
                listener.Stop();
            listener.Close();
            try
            {
                await accept.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or TaskCanceledException or TimeoutException)
            {
                TestContext.WriteLine($"Docs CDP listener stopped: {ex.GetType().Name}");
            }
        }

        Assert.That(File.Exists(output), Is.True);
        Assert.That(File.ReadAllBytes(output)[0], Is.EqualTo(0x89));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
    }

    private static async Task AcceptCdpAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (listener.IsListening && !cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            _ = HandleCdpContextAsync(context, cancellationToken);
        }
    }

    private static async Task HandleCdpContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        if (context.Request.Url?.AbsolutePath.Contains("json/list", StringComparison.Ordinal) == true)
        {
            var payload =
                "[{\"id\":\"page\",\"url\":\""
                + DebugLayout.DocsUrl
                + DebugLayout.DocsHomePath
                + "\",\"webSocketDebuggerUrl\":\"ws://127.0.0.1:"
                + ChromiumDocsPageDriver.DebuggingPort
                + "/devtools\"}]";
            var bytes = Encoding.UTF8.GetBytes(payload);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
            context.Response.Close();
            return;
        }

        if (!context.Request.IsWebSocketRequest)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        var socketContext = await context.AcceptWebSocketAsync(null);
        using var socket = socketContext.WebSocket;
        var buffer = new byte[8192];
        using var memory = new MemoryStream();
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var received = await socket.ReceiveAsync(buffer, cancellationToken);
            if (received.MessageType == WebSocketMessageType.Close)
                break;
            memory.Write(buffer, 0, received.Count);
            if (!received.EndOfMessage)
                continue;

            var response = CdpResponse(Encoding.UTF8.GetString(memory.ToArray()));
            memory.SetLength(0);
            var bytes = Encoding.UTF8.GetBytes(response);
            var split = Math.Max(1, bytes.Length / 2);
            await socket.SendAsync(bytes.AsMemory(0, split), WebSocketMessageType.Text, false, cancellationToken);
            await socket.SendAsync(bytes.AsMemory(split), WebSocketMessageType.Text, true, cancellationToken);
        }
    }

    private static string CdpResponse(string request)
    {
        using var document = JsonDocument.Parse(request);
        var id = document.RootElement.GetProperty("id").GetInt32();
        var method = document.RootElement.GetProperty("method").GetString();
        if (method == "Runtime.evaluate"
            && document.RootElement.GetProperty("params").GetProperty("expression").GetString() is { } expression
            && expression.Contains("scrollHeight", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new
            {
                id,
                result = new { result = new { type = "object", value = new { width = 800, height = 2400 } } }
            });
        }

        if (method == "Page.captureScreenshot")
        {
            const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
            return JsonSerializer.Serialize(new { id, result = new { data = png } });
        }

        return JsonSerializer.Serialize(new { id, result = new { result = new { type = "string", value = "ok" } } });
    }
}
