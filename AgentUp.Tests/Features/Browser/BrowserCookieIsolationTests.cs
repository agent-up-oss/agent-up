using System.Net.Http.Json;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Applications.Http;
using AgentUp.Desktop.Features.Console.Http;
using AgentUp.Desktop.Features.Ports.Http;
using AgentUp.Desktop.Features.Workspaces.Http;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Tests.Support;

namespace AgentUp.Tests.Features.Browser;

// These tests require a real display (X11/Wayland) and WebKitGTK.
// Run them manually: dotnet test --filter Category=E2E
// They currently FAIL because WebKitGTK shares one web context across all WebView instances.
// They will pass once per-workspace WebKitWebContext isolation is implemented.
[Explicit("Requires display + WebKitGTK — not for CI")]
[Category("E2E")]
public sealed class BrowserCookieIsolationTests
{
    private CookieTestServer _server = null!;

    [SetUp]
    public void SetUp() => _server = new CookieTestServer();

    [TearDown]
    public void TearDown() => _server.Dispose();

    // Scenario 1: Two workspaces, one app each.
    // WS1 sets session=ws1, then WS2 sets session=ws2 for the same cookie name.
    // WS1's stored cookie must not be overwritten by WS2's write.
    [AvaloniaTest]
    public async Task Workspace_cookies_are_isolated_from_each_other()
    {
        var port = _server.Port;
        var window = await LaunchWindowAsync([MakeWorkspace("ws-1", port), MakeWorkspace("ws-2", port)]);

        window.NavigateTo("ws-1", $"http://localhost:{port}/set/session/ws1");
        await _server.WaitForRequestAsync("/set/session/ws1");

        window.NavigateTo("ws-2", $"http://localhost:{port}/set/session/ws2");
        await _server.WaitForRequestAsync("/set/session/ws2");

        window.NavigateTo("ws-1", $"http://localhost:{port}/check");
        var req = await _server.WaitForRequestAsync("/check");

        Assert.That(req.CookieHeader, Does.Contain("session=ws1"),
            "WS1 cookie must not be overwritten by WS2's write to the same name");
    }

    // Scenario 2: Two workspaces each have a login app and a page app.
    // After WS1 logs in, WS1's page sees the login cookie.
    // WS2's page must not see that cookie (isolated sessions).
    [AvaloniaTest]
    public async Task Login_cookie_does_not_leak_to_other_workspace()
    {
        var port = _server.Port;
        var window = await LaunchWindowAsync([MakeWorkspace("ws-1", port), MakeWorkspace("ws-2", port)]);

        // WS1 logs in (server responds with Set-Cookie: logged_in=true)
        window.NavigateTo("ws-1", $"http://localhost:{port}/set/logged_in/true");
        await _server.WaitForRequestAsync("/set/logged_in/true");

        // WS1's page sees the cookie
        window.NavigateTo("ws-1", $"http://localhost:{port}/check");
        var ws1Req = await _server.WaitForRequestAsync("/check");
        Assert.That(ws1Req.CookieHeader, Does.Contain("logged_in=true"),
            "WS1 should see its own login cookie");

        // WS2's page must not see WS1's cookie
        window.NavigateTo("ws-2", $"http://localhost:{port}/check");
        var ws2Req = await _server.WaitForRequestAsync("/check");
        Assert.That(ws2Req.CookieHeader, Is.Null.Or.Not.Contain("logged_in"),
            "WS2 must not see WS1's login cookie");
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

    private static async Task<MainWindow> LaunchWindowAsync(List<WorkspaceDto> workspaces)
    {
        var handler = new E2EFakeHttpHandler(workspaces);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var vm = new MainViewModel(new WorkspaceApiClient(http), new ConsoleApiClient(http));
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await vm.InitializeAsync();
        await Task.Delay(200); // allow Avalonia to process layout and WebKitGTK to initialise
        return window;
    }
}

file sealed class E2EFakeHttpHandler(List<WorkspaceDto> workspaces) : HttpMessageHandler
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
