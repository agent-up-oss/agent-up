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

// These tests drive the real MainWindow with the platform WebView backend selected
// by the AgentUp.Fixtures.* adapter for the current test runner OS.
//
// Each NavigateTo call is dispatched to the UI thread, then the test thread waits
// for the HTTP server to receive the request. This keeps the UI thread free to
// process GTK/WebKit events during the wait.
//
// Run explicitly:
//   dotnet test AgentUp.Tests/ --filter "Category=E2E"
[TestFixture, Category("E2E")]
public sealed class BrowserCookieIsolationTests
{
    private CookieTestServer _server = null!;
    private MainWindow? _window;
    private string _profileRoot = null!;
    private string _savedProfileRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _profileRoot = Path.Join(Path.GetTempPath(), $"agentup-e2e-cookie-{Guid.NewGuid()}");
        _savedProfileRoot = BrowserUrlStore.RootPath;
        BrowserUrlStore.RootPath = _profileRoot;
        _server = new CookieTestServer();
    }

    [TearDown]
    public async Task TearDown()
    {
        var w = _window;
        _window = null;
        if (w is not null)
            await Dispatcher.UIThread.InvokeAsync(() => w.Close());

        _server.Dispose();
        BrowserUrlStore.RootPath = _savedProfileRoot;
    }

    // Scenario 1: Two workspaces, two apps each.
    // WS1 sets session=ws1, then WS2 sets session=ws2 for the same cookie name.
    // WS1's cookie must not be overwritten by WS2.
    [Test, CancelAfter(60000)]
    public async Task Workspace_cookies_are_isolated_from_each_other()
    {
        int port = _server.Port;
        _window = await LaunchWindowAsync([MakeWorkspace("ws-1", port), MakeWorkspace("ws-2", port)]);

        Navigate("ws-1", $"http://localhost:{port}/set/session/ws1");
        await _server.WaitForRequestAsync("/set/session/ws1");
        await WaitForDocumentCookieAsync("ws-1", "session=ws1");

        Navigate("ws-2", $"http://localhost:{port}/set/session/ws2");
        await _server.WaitForRequestAsync("/set/session/ws2");
        await WaitForDocumentCookieAsync("ws-2", "session=ws2");

        var req = await WaitForCookieHeaderAsync("ws-1", "ws1-after-ws2", contains: "session=ws1");
        Assert.That(req.CookieHeader, Does.Contain("session=ws1"),
            "WS1's cookie must not be overwritten by WS2's write to the same name");
    }

    // Scenario 2: WS1 logs in (sets a cookie). WS2 must not see that cookie.
    [Test, CancelAfter(60000)]
    public async Task Login_cookie_does_not_leak_to_other_workspace()
    {
        int port = _server.Port;
        _window = await LaunchWindowAsync([MakeWorkspace("ws-1", port), MakeWorkspace("ws-2", port)]);

        Navigate("ws-1", $"http://localhost:{port}/set/logged_in/true");
        await _server.WaitForRequestAsync("/set/logged_in/true");
        await WaitForDocumentCookieAsync("ws-1", "logged_in=true");

        var ws1Req = await WaitForCookieHeaderAsync("ws-1", "ws1-login", contains: "logged_in=true");
        Assert.That(ws1Req.CookieHeader, Does.Contain("logged_in=true"),
            "WS1's page must see its own login cookie");

        Navigate("ws-2", $"http://localhost:{port}/check/ws2-login");
        var ws2Req = await _server.WaitForRequestAsync("/check/ws2-login");
        Assert.That(ws2Req.CookieHeader, Is.Null.Or.Not.Contain("logged_in"),
            "WS2's page must not see WS1's login cookie");
    }

    // Scenario 3: Full 2×2 isolation.
    //
    // Two workspaces, two apps each. Within a workspace, App1 and App2 share
    // the same browser profile so cookies set by one are visible to the other.
    // Across workspaces, the same cookie name is used but the values never
    // collide — each workspace has its own isolated profile.
    //
    //   WS1/App1 → set session=ws1
    //   WS1/App2 → check         → must see session=ws1  (intra-workspace sharing)
    //   WS2/App1 → set session=ws2
    //   WS2/App2 → check         → must see session=ws2  (intra-workspace sharing)
    //   WS1/App2 → check again   → must still see session=ws1, not ws2  (inter-workspace isolation)
    [Test, CancelAfter(90000)]
    public async Task Two_apps_within_workspace_share_profile_isolated_from_other_workspace()
    {
        int port = _server.Port;
        _window = await LaunchWindowAsync([MakeWorkspace("ws-1", port), MakeWorkspace("ws-2", port)]);

        // WS1/App1 sets a cookie.
        Navigate("ws-1", $"http://localhost:{port}/set/session/ws1");
        await _server.WaitForRequestAsync("/set/session/ws1");
        await WaitForDocumentCookieAsync("ws-1", "session=ws1");

        // WS1/App2 reads — must share the cookie from App1 (same browser profile).
        var ws1App2First = await WaitForCookieHeaderAsync("ws-1", "ws1-app2-first", contains: "session=ws1");
        Assert.That(ws1App2First.CookieHeader, Does.Contain("session=ws1"),
            "App2 of WS1 must see the cookie set by App1 of WS1 — they share a browser profile");

        // WS2/App1 sets the same-named cookie with a different value.
        Navigate("ws-2", $"http://localhost:{port}/set/session/ws2");
        await _server.WaitForRequestAsync("/set/session/ws2");
        await WaitForDocumentCookieAsync("ws-2", "session=ws2");

        // WS2/App2 reads — must share WS2's cookie, not WS1's.
        var ws2App2 = await WaitForCookieHeaderAsync("ws-2", "ws2-app2", contains: "session=ws2", excludes: "session=ws1");
        Assert.That(ws2App2.CookieHeader, Does.Contain("session=ws2"),
            "App2 of WS2 must see the cookie set by App1 of WS2 — they share a browser profile");
        Assert.That(ws2App2.CookieHeader, Does.Not.Contain("session=ws1"),
            "WS2 must not see WS1's cookie value");

        // WS1/App2 re-checks — must still see ws1, not contaminated by WS2.
        var ws1App2Second = await WaitForCookieHeaderAsync("ws-1", "ws1-app2-second", contains: "session=ws1", excludes: "session=ws2");
        Assert.That(ws1App2Second.CookieHeader, Does.Contain("session=ws1"),
            "WS1's cookie must survive WS2 writing the same cookie name");
        Assert.That(ws1App2Second.CookieHeader, Does.Not.Contain("session=ws2"),
            "WS1 must not see WS2's cookie value");
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

        // GTK realizes the window and WebKit starts its child processes (NetworkProcess,
        // WebProcess) lazily on first WebView creation. Allow time for these bootstraps
        // before any Navigate call triggers new NativeWebView() from a dispatcher callback.
        await Task.Delay(3000);

        return window;
    }

    private void Navigate(string workspaceId, string url)
    {
        Dispatcher.UIThread.Post(() => _window!.NavigateTo(workspaceId, url));
    }

    private async Task<CookieTestServer.ReceivedRequest> WaitForCookieHeaderAsync(
        string workspaceId,
        string checkName,
        string contains,
        string? excludes = null,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        CookieTestServer.ReceivedRequest? lastRequest = null;
        var attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            var path = $"/check/{checkName}/{attempt++}";
            Navigate(workspaceId, $"http://localhost:{_server.Port}{path}");
            lastRequest = await _server.WaitForRequestAsync(path, TimeSpan.FromSeconds(5));

            if (lastRequest.CookieHeader?.Contains(contains, StringComparison.Ordinal) == true
                && (excludes is null || !lastRequest.CookieHeader.Contains(excludes, StringComparison.Ordinal)))
                return lastRequest;

            await Task.Delay(250);
        }

        Assert.Fail(
            $"Expected cookie header containing '{contains}'"
            + (excludes is null ? "" : $" and excluding '{excludes}'")
            + $" for workspace '{workspaceId}', but last header was '{lastRequest?.CookieHeader ?? "(null)"}'.");

        throw new InvalidOperationException("Assert.Fail did not throw.");
    }

    private async Task WaitForDocumentCookieAsync(
        string workspaceId,
        string contains,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        string? lastCookie = null;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                lastCookie = await _window!.EvalAsync(workspaceId, "document.cookie");
                lastException = null;
                if (lastCookie?.Contains(contains, StringComparison.Ordinal) == true)
                    return;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(250);
        }

        Assert.Fail(
            $"Expected document.cookie containing '{contains}' for workspace '{workspaceId}',"
            + $" but last value was '{lastCookie ?? "(null)"}'"
            + (lastException is null ? "." : $" and last error was '{lastException.Message}'."));
    }

    // Each workspace has two apps (App1 and App2) both pointing at the same
    // cookie test server — simulating two web apps in the same workspace.
    private static WorkspaceDto MakeWorkspace(string id, int port) =>
        new(id, id, $"/repo/{id}", $"/worktrees/{id}", "main", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("App1", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 8080, port)]
                },
                new ApplicationDto("App2", "cmd", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto(null, 8081, port)]
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
