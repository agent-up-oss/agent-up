using System.Net.Http.Json;
using AgentUp.Desktop.Features.Applications.Http;
using AgentUp.Desktop.Features.Console.Http;
using AgentUp.Desktop.Features.Ports.Http;
using AgentUp.Desktop.Features.Workspaces.Http;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Tests.Support;
using Avalonia.Threading;

namespace AgentUp.Tests.Features.Browser;

// These tests drive the real MainWindow with real WebKitGTK and a real X display
// (provided by XvfbManager when DISPLAY is not already set).
//
// Each NavigateTo call is dispatched briefly to the UI thread, then the test
// thread waits for the HTTP server to receive the request. This keeps the UI
// thread free to process GTK/WebKit events during the wait — if the wait also
// ran on the UI thread there would be a deadlock (WebKit needs the UI thread to
// drive its network stack).
//
// Tests currently FAIL because all WebView instances share one WebKitWebContext,
// so cookies leak between workspaces. They are the TDD spec for the isolation
// feature; they will pass once per-workspace WebKitWebContexts are implemented.
[TestFixture]
public sealed class BrowserCookieIsolationTests
{
    private CookieTestServer _server = null!;
    private MainWindow? _window;

    [SetUp]
    public void SetUp() => _server = new CookieTestServer();

    [TearDown]
    public void TearDown()
    {
        _server.Dispose();
        var w = _window;
        _window = null;
        if (w is not null)
            Dispatcher.UIThread.Post(() => w.Close());
    }

    // Scenario 1: Two workspaces, one app each.
    // WS1 sets session=ws1, then WS2 sets session=ws2 for the same cookie name.
    // If sessions are isolated, WS1's WebKit still sends session=ws1 afterward.
    [Test, CancelAfter(60000)]
    public async Task Workspace_cookies_are_isolated_from_each_other()
    {
        int port = _server.Port;
        _window = await LaunchWindowAsync([MakeWorkspace("ws-1", port), MakeWorkspace("ws-2", port)]);

        Navigate("ws-1", $"http://localhost:{port}/set/session/ws1");
        await _server.WaitForRequestAsync("/set/session/ws1");

        Navigate("ws-2", $"http://localhost:{port}/set/session/ws2");
        await _server.WaitForRequestAsync("/set/session/ws2");

        Navigate("ws-1", $"http://localhost:{port}/check");
        var req = await _server.WaitForRequestAsync("/check");

        Assert.That(req.CookieHeader, Does.Contain("session=ws1"),
            "WS1's cookie must not be overwritten by WS2's write to the same name");
    }

    // Scenario 2: Two workspaces each have a login app and a page app.
    // Logging in via WS1 sets a cookie that WS1's page can read.
    // WS2's page must not see that cookie after a reload.
    [Test, CancelAfter(60000)]
    public async Task Login_cookie_does_not_leak_to_other_workspace()
    {
        int port = _server.Port;
        _window = await LaunchWindowAsync([MakeWorkspace("ws-1", port), MakeWorkspace("ws-2", port)]);

        Navigate("ws-1", $"http://localhost:{port}/set/logged_in/true");
        await _server.WaitForRequestAsync("/set/logged_in/true");

        Navigate("ws-1", $"http://localhost:{port}/check");
        var ws1Req = await _server.WaitForRequestAsync("/check");
        Assert.That(ws1Req.CookieHeader, Does.Contain("logged_in=true"),
            "WS1's page must see its own login cookie");

        Navigate("ws-2", $"http://localhost:{port}/check");
        var ws2Req = await _server.WaitForRequestAsync("/check");
        Assert.That(ws2Req.CookieHeader, Is.Null.Or.Not.Contain("logged_in"),
            "WS2's page must not see WS1's login cookie");
    }

    // Window creation runs on the UI thread (Avalonia controls must be created there).
    // WaitForRequestAsync runs on the test thread so the UI thread stays free to
    // drive WebKit's network stack.
    private async Task<MainWindow> LaunchWindowAsync(List<WorkspaceDto> workspaces)
    {
        var window = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var handler = new FakeHttpHandler(workspaces);
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
            var vm = new MainViewModel(new WorkspaceApiClient(http), new ConsoleApiClient(http));
            var window = new MainWindow { DataContext = vm };
            window.Show();
            await vm.InitializeAsync();
            return window;
        });

        // GTK realizes the window asynchronously. WebKit also starts its child processes
        // (NetworkProcess, WebProcess) lazily on first WebView creation, which requires
        // the GLib main loop to be idle (not inside a dispatch callback). Allow 3s for
        // these one-time bootstraps to complete before any Navigate call triggers
        // new WebView() from a dispatcher callback.
        await Task.Delay(3000);

        return window;
    }

    // Posts a NavigateTo call to the UI thread and returns once it has been dispatched.
    private void Navigate(string workspaceId, string url)
    {
        Dispatcher.UIThread.Post(() => _window!.NavigateTo(workspaceId, url));
    }

    private static WorkspaceDto MakeWorkspace(string id, int port) =>
        new(id, id, $"/repo/{id}", $"/worktrees/{id}", "main", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("App", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 8080, port)]
                }
            ]
        };
}

file sealed class FakeHttpHandler(List<WorkspaceDto> workspaces) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        if (path == "/api/workspaces")
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = JsonContent.Create(workspaces) });

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = JsonContent.Create(Array.Empty<string>()) });
    }
}
