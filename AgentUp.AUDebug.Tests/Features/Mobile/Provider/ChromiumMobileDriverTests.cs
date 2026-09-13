using System.Net;
using System.Net.WebSockets;
using System.Text;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Mobile.Provider;

[TestFixture]
[NonParallelizable]
public sealed class ChromiumMobileDriverTests
{
    [Test]
    public void Login_whenCanceled_killsBrowser()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-mobile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumMobileDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.LoginAsync(DebugLayout.ServerUrl, "test", timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started, Has.Count.EqualTo(1));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
        Assert.That(processes.Started[0].FileName, Is.EqualTo("chromium"));
        Assert.That(processes.Started[0].Arguments, Does.Contain($"{DebugLayout.MobileUrl}/connect"));
    }

    [Test]
    public void Login_fallsBackToNixShellWhenChromiumIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-mobile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var driver = new ChromiumMobileDriver(processes, new FakeEnvironment(), new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.LoginAsync(DebugLayout.ServerUrl, "test", timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("nix-shell"));
        Assert.That(processes.Started[0].Arguments, Does.Contain("chromium"));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
    }

    [Test]
    public void Login_fallsBackToChromiumBrowserThenChrome()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-mobile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium-browser"] = "/usr/bin/chromium-browser";
        var driver = new ChromiumMobileDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.LoginAsync(DebugLayout.ServerUrl, "test", timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("chromium-browser"));

        processes.Started.Clear();
        environment.Executables.Clear();
        environment.Executables["google-chrome"] = "/usr/bin/google-chrome";
        Assert.That(
            async () => await driver.LoginAsync(DebugLayout.ServerUrl, "test", timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].FileName, Is.EqualTo("google-chrome"));
    }

    [Test]
    public void Login_retriesWhileTheDebuggerPortIsClosed()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-mobile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumMobileDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        Assert.That(
            async () => await driver.LoginAsync(DebugLayout.ServerUrl, "test", timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
    }

    [Test]
    public void PageWorldDestroyed_matchesCdpNavigationError()
    {
        Assert.That(
            ChromiumMobileDriver.IsPageWorldDestroyed("Mobile login CDP failed: {\"code\":-32000,\"message\":\"Execution context was destroyed.\"}"),
            Is.True);
        Assert.That(ChromiumMobileDriver.IsPageWorldDestroyed("button missing"), Is.False);
    }

    [Test]
    public void CaptureAgent_whenCanceled_killsBrowser()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-mobile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/bin/chromium";
        var driver = new ChromiumMobileDriver(processes, environment, new FakePathValidator(root));
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        Assert.That(
            async () => await driver.CaptureAgentAsync(Path.Join(root, ".git", "agent-up", "au-debug", "screenshots", "mobile.png"), timeout.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(processes.Started[0].Arguments, Does.Contain($"{DebugLayout.MobileUrl}/"));
        Assert.That(processes.Killed, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Login_opensDebuggerWhenJsonListReturnsAWebsocket()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{ChromiumMobileDriver.DebuggingPort}/");
        listener.Start();
        var serving = ServeDebuggerListAsync(
            listener,
            """[{"webSocketDebuggerUrl":"ws://127.0.0.1:1/devtools/page/x"}]""");

        try
        {
            var root = Path.Join(Path.GetTempPath(), "au-debug-mobile", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Join(root, ".git"));
            var processes = new FakeProcessRunner();
            var environment = new FakeEnvironment();
            environment.Executables["chromium"] = "/bin/chromium";
            var driver = new ChromiumMobileDriver(processes, environment, new FakePathValidator(root));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            Assert.That(
                async () => await driver.LoginAsync(DebugLayout.ServerUrl, "test", timeout.Token),
                Throws.InstanceOf<WebSocketException>()
                    .Or.InstanceOf<HttpRequestException>()
                    .Or.InstanceOf<OperationCanceledException>());
            Assert.That(processes.Killed, Has.Count.EqualTo(1));
        }
        finally
        {
            listener.Stop();
            try
            {
                await serving;
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                TestContext.Out.WriteLine($"debugger list listener stopped: {ex.GetType().Name}");
            }
        }
    }

    private static async Task ServeDebuggerListAsync(HttpListener listener, string json)
    {
        while (listener.IsListening)
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

            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.Close();
        }
    }
}
