using System.Net;
using System.Net.Http.Json;
using System.Text;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.Factories;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Features.Workspaces.Repositories;
using AgentUp.Tests.Support;
using Avalonia.Threading;

namespace AgentUp.Tests.Features.Browser;

// These tests verify that the WebView actually renders HTML served by a real HTTP server.
// The port sub-tab is selected through the ViewModel so the WebView is visible in the window
// (not hidden behind the Console tab). A DOMContentLoaded beacon tells us when to query the DOM.
//
// Run explicitly:
//   dotnet test AgentUp.Tests/ --filter "Category=E2E"
[TestFixture, Category("E2E")]
public sealed class BrowserRenderingTests
{
    private HtmlTestServer _server = null!;
    private MainWindow? _window;
    private string _profileRoot = null!;
    private string _savedProfileRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _profileRoot = Path.Join(Path.GetTempPath(), $"agentup-e2e-rendering-{Guid.NewGuid()}");
        _savedProfileRoot = BrowserUrlStore.RootPath;
        BrowserUrlStore.RootPath = _profileRoot;
        _server = new HtmlTestServer("""
            <!DOCTYPE html>
            <html><body>
              <h1 id="title">Agent-Up Rendered</h1>
              <p id="static-para">Static paragraph</p>
              <script>
                var div = document.createElement('div');
                div.id = 'injected';
                div.textContent = 'JS was here';
                document.body.appendChild(div);
                fetch('/beacon');
              </script>
            </body></html>
            """);
    }

    [TearDown]
    public async Task TearDown()
    {
        var w = _window;
        _window = null;
        if (w is not null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => w.Close());
            await FlushDispatcherAsync();
        }

        _server.Dispose();
        BrowserUrlStore.RootPath = _savedProfileRoot;
    }

    [Test, CancelAfter(60000)]
    public async Task Browser_rendersStaticHtmlElement()
    {
        await LaunchAndNavigateAsync();
        await _server.WaitForBeaconAsync();

        var text = await _window!.EvalAsync("ws-1", "document.getElementById('title').textContent");
        Assert.That(text, Is.EqualTo("Agent-Up Rendered"));
    }

    [Test, CancelAfter(60000)]
    public async Task Browser_rendersElementInjectedByJavaScript()
    {
        await LaunchAndNavigateAsync();
        await _server.WaitForBeaconAsync();

        var text = await _window!.EvalAsync("ws-1", "document.getElementById('injected').textContent");
        Assert.That(text, Is.EqualTo("JS was here"));
    }

    [Test, CancelAfter(60000)]
    public async Task Browser_returnsNull_forMissingElement()
    {
        await LaunchAndNavigateAsync();
        await _server.WaitForBeaconAsync();

        // querySelector returns null in JS; InvokeScript returns "null" as a string.
        var result = await _window!.EvalAsync("ws-1",
            "var el = document.getElementById('does-not-exist'); el ? el.textContent : null");
        Assert.That(result, Is.Null.Or.EqualTo("null"));
    }

    // Launches the window, selects the port sub-tab immediately after init (so the browser
    // tab is visible during the GTK warm-up delay), then waits for GTK to stabilise.
    private async Task LaunchAndNavigateAsync()
    {
        _window = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var handler = new RenderingFakeHttpHandler([MakeWorkspace("ws-1", _server.Port)]);
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var vm = MainViewModelFactory.Create(new AgentUp.Desktop.Features.Workspaces.Providers.WorkspaceApiClient(http),
                                       new AgentUp.Desktop.Features.Console.Providers.ConsoleApiClient(http));
            var w = new MainWindow { DataContext = vm };
            w.Show();
            await vm.InitializeAsync();

            var portTab = vm.SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault();
            if (portTab is not null)
                vm.SelectedSubTab = portTab;

            return w;
        });

        await Task.Delay(3000);
    }

    private static WorkspaceDto MakeWorkspace(string id, int port) =>
        new(id, id, $"/repo/{id}", $"/worktrees/{id}", "main", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("App", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, port, port)]
                }
            ]
        };

    private static async Task FlushDispatcherAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

}

sealed class HtmlTestServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _html;
    private readonly SemaphoreSlim _beaconSignal = new(0);

    public int Port { get; }

    public HtmlTestServer(string html)
    {
        _html = html;
        Port = FindFreePort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
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

            if (path == "/beacon")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                _beaconSignal.Release();
                continue;
            }

            // Serve the HTML for any other path.
            var body = Encoding.UTF8.GetBytes(_html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = body.Length;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        }
    }

    public async Task WaitForBeaconAsync(TimeSpan? timeout = null)
    {
        var ok = await _beaconSignal.WaitAsync(timeout ?? TimeSpan.FromSeconds(30));
        if (!ok) throw new TimeoutException("Browser never sent the /beacon request");
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { _ = ex; }
    }
}

sealed class RenderingFakeHttpHandler(List<WorkspaceDto> workspaces) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri?.AbsolutePath == "/api/workspaces")
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = JsonContent.Create(workspaces) });
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = JsonContent.Create(Array.Empty<string>()) });
    }
}
