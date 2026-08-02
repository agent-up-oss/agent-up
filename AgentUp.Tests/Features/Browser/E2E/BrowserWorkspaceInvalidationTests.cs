using System.Net;
using System.Text;
using System.Text.Json;
using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Console.Providers;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.Repositories;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Tests.Support;
using Avalonia.Threading;

namespace AgentUp.Tests.Features.Browser.E2E;

[TestFixture, Category("E2E")]
public sealed class BrowserWorkspaceInvalidationTests
{
    private InvalidationHtmlServer _ws1Initial = null!;
    private InvalidationHtmlServer _ws1Updated = null!;
    private InvalidationHtmlServer _ws2Initial = null!;
    private InvalidationHtmlServer _ws2Updated = null!;
    private MainWindow? _window;
    private ScopedWorkspaceFakeServer? _fakeServer;
    private HttpClient? _http;
    private string _profileRoot = null!;
    private string _savedProfileRoot = null!;
    private string? _savedServerUrl;

    [SetUp]
    public void SetUp()
    {
        _profileRoot = Path.Join(Path.GetTempPath(), $"agentup-e2e-invalidation-{Guid.NewGuid()}");
        _savedProfileRoot = BrowserUrlStore.RootPath;
        BrowserUrlStore.RootPath = _profileRoot;
        _savedServerUrl = Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL");
        _ws1Initial = NewServer("ws-1 initial");
        _ws1Updated = NewServer("ws-1 updated");
        _ws2Initial = NewServer("ws-2 initial");
        _ws2Updated = NewServer("ws-2 updated");
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

        _fakeServer?.Dispose();
        _http?.Dispose();
        _http = null;
        _ws1Initial.Dispose();
        _ws1Updated.Dispose();
        _ws2Initial.Dispose();
        _ws2Updated.Dispose();
        if (_savedServerUrl is null)
            Environment.SetEnvironmentVariable("AGENTUP_SERVER_URL", null);
        else
            Environment.SetEnvironmentVariable("AGENTUP_SERVER_URL", _savedServerUrl);
        BrowserUrlStore.RootPath = _savedProfileRoot;
    }

    [Test, CancelAfter(90000)]
    public async Task Selected_workspace_invalidation_updates_visible_browser_to_new_port()
    {
        _window = await LaunchWindowAsync([
            MakeWorkspace("ws-1", _ws1Initial.Port),
            MakeWorkspace("ws-2", _ws2Initial.Port)
        ]);
        await _ws1Initial.WaitForBeaconAsync();

        _fakeServer!.ClearRequests();
        _fakeServer.SetWorkspace(MakeWorkspace("ws-1", _ws1Updated.Port));
        await _fakeServer.EmitWorkspaceEventAsync("ws-1", "Running", [("App", "Running")]);

        await _ws1Updated.WaitForBeaconAsync();

        var state = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = (MainViewModel)_window!.DataContext!;
            return new
            {
                Address = vm.AddressBarUrl,
                SelectedPort = ((PortSubTabViewModel)vm.SelectedSubTab!).AllocatedPort
            };
        });

        Assert.That(state.Address, Is.EqualTo($"http://localhost:{_ws1Updated.Port}/"));
        Assert.That(state.SelectedPort, Is.EqualTo(_ws1Updated.Port));
        Assert.That(_fakeServer.RequestPaths, Does.Contain("/api/workspaces/ws-1"));
        Assert.That(_fakeServer.RequestPaths, Does.Not.Contain("/api/workspaces"));
        Assert.That(_fakeServer.RequestPaths, Does.Not.Contain("/api/workspaces/ws-2"));
    }

    [Test, CancelAfter(90000)]
    public async Task Nonselected_workspace_invalidation_does_not_reload_visible_browser()
    {
        _window = await LaunchWindowAsync([
            MakeWorkspace("ws-1", _ws1Initial.Port),
            MakeWorkspace("ws-2", _ws2Initial.Port)
        ]);
        await _ws1Initial.WaitForBeaconAsync();
        var initialWs1BeaconCount = _ws1Initial.BeaconCount;

        _fakeServer!.ClearRequests();
        _fakeServer.SetWorkspace(MakeWorkspace("ws-2", _ws2Updated.Port));
        await _fakeServer.EmitWorkspaceEventAsync("ws-2", "Running", [("App", "Running")]);

        await WaitForRequestPathAsync("/api/workspaces/ws-2");
        await Task.Delay(1000);

        var state = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = (MainViewModel)_window!.DataContext!;
            return new
            {
                WorkspaceId = vm.Sidebar.SelectedWorkspace!.Id,
                Address = vm.AddressBarUrl,
                SelectedPort = ((PortSubTabViewModel)vm.SelectedSubTab!).AllocatedPort
            };
        });

        Assert.That(state.WorkspaceId, Is.EqualTo("ws-1"));
        Assert.That(state.Address, Is.EqualTo($"http://localhost:{_ws1Initial.Port}/"));
        Assert.That(state.SelectedPort, Is.EqualTo(_ws1Initial.Port));
        Assert.That(_ws1Initial.BeaconCount, Is.EqualTo(initialWs1BeaconCount));
        Assert.That(_ws2Updated.BeaconCount, Is.Zero);
        Assert.That(_fakeServer.RequestPaths, Does.Contain("/api/workspaces/ws-2"));
        Assert.That(_fakeServer.RequestPaths, Does.Not.Contain("/api/workspaces"));
        Assert.That(_fakeServer.RequestPaths, Does.Not.Contain("/api/workspaces/ws-1"));
    }

    private async Task<MainWindow> LaunchWindowAsync(List<WorkspaceDto> workspaces)
    {
        _fakeServer = new ScopedWorkspaceFakeServer(workspaces);
        Environment.SetEnvironmentVariable("AGENTUP_SERVER_URL", _fakeServer.BaseUrl);

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            _http = new HttpClient { BaseAddress = new Uri(_fakeServer.BaseUrl) };
            var vm = MainViewModelFactory.Create(new WorkspaceApiClient(_http), new ConsoleApiClient(_http));
            var window = new MainWindow { DataContext = vm };
            window.BrowserProbe = _ => Task.FromResult<string?>(null);
            window.Show();
            await vm.InitializeAsync();

            var portTab = vm.SubTabs.OfType<PortSubTabViewModel>().FirstOrDefault();
            if (portTab is not null)
                vm.SelectedSubTab = portTab;

            if (vm.Sidebar.SelectedWorkspace is { } selectedWorkspace
                && portTab is not null)
                window.NavigateTo(selectedWorkspace.Id, $"http://localhost:{portTab.AllocatedPort}/");

            return window;
        });
    }

    private async Task WaitForRequestPathAsync(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (_fakeServer!.RequestPaths.Contains(path))
                return;

            await Task.Delay(100);
        }

        Assert.Fail($"Expected request to '{path}' was not observed.");
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

    private static InvalidationHtmlServer NewServer(string title) =>
        new($"""
            <!DOCTYPE html>
            <html><body>
              <h1 id="title">{WebUtility.HtmlEncode(title)}</h1>
              <script>fetch('/beacon');</script>
            </body></html>
            """);

    private static async Task FlushDispatcherAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }
}

sealed class InvalidationHtmlServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _html;
    private readonly SemaphoreSlim _beaconSignal = new(0);
    private int _beaconCount;

    public int Port { get; }
    public int BeaconCount => Volatile.Read(ref _beaconCount);

    public InvalidationHtmlServer(string html)
    {
        _html = html;
        Port = FindFreePort();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        _listener.Start();
        _ = ListenAsync();
    }

    private async Task ListenAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }

            if (context.Request.Url?.AbsolutePath == "/beacon")
            {
                Interlocked.Increment(ref _beaconCount);
                context.Response.StatusCode = 204;
                context.Response.Close();
                _beaconSignal.Release();
                continue;
            }

            var body = Encoding.UTF8.GetBytes(_html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = body.Length;
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }
    }

    public async Task WaitForBeaconAsync(TimeSpan? timeout = null)
    {
        var ok = await _beaconSignal.WaitAsync(timeout ?? TimeSpan.FromSeconds(15));
        if (!ok)
            throw new TimeoutException($"No browser beacon received by test server on port {Port}.");
    }

    private static int FindFreePort()
    {
        using var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    public void Dispose()
    {
        try { _listener.Stop(); }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            TestContext.Progress.WriteLine(ex.Message);
        }
    }
}

sealed class ScopedWorkspaceFakeServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Lock _lock = new();
    private readonly List<Stream> _eventStreams = [];
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private List<WorkspaceDto> _workspaces = [];
    private readonly List<string> _requestPaths = [];

    public string BaseUrl { get; }
    public IReadOnlyList<string> RequestPaths
    {
        get
        {
            lock (_lock)
                return _requestPaths.ToList();
        }
    }

    public ScopedWorkspaceFakeServer(List<WorkspaceDto> workspaces) : this()
    {
        _workspaces = workspaces;
    }

    private ScopedWorkspaceFakeServer()
    {
        var port = FindFreePort();
        BaseUrl = $"http://localhost:{port}";
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();
        _ = ListenAsync();
    }

    public void SetWorkspace(WorkspaceDto workspace)
    {
        lock (_lock)
        {
            var next = _workspaces.Where(w => w.Id != workspace.Id).ToList();
            next.Add(workspace);
            _workspaces = next;
        }
    }

    public void ClearRequests()
    {
        lock (_lock)
            _requestPaths.Clear();
    }

    public async Task EmitWorkspaceEventAsync(string workspaceId, string state, IReadOnlyList<(string Name, string State)> apps)
    {
        await WaitForEventSubscriberAsync();

        var applications = apps
            .Select(app => new { name = app.Name, state = app.State })
            .ToArray();
        var json = JsonSerializer.Serialize(new
        {
            workspaceId,
            state,
            applications
        });
        var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
        List<Stream> streams;
        lock (_lock)
            streams = _eventStreams.ToList();

        foreach (var stream in streams)
        {
            await stream.WriteAsync(bytes);
            await stream.FlushAsync();
        }
    }

    private async Task WaitForEventSubscriberAsync()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            lock (_lock)
            {
                if (_eventStreams.Count > 0)
                    return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("Desktop did not connect to the workspace event stream.");
    }

    private async Task ListenAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }

            _ = HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "";
        if (path != "/api/workspaces/events")
        {
            lock (_lock)
                _requestPaths.Add(path);
        }

        if (path == "/api/workspaces/events")
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/event-stream";
            context.Response.SendChunked = true;
            lock (_lock)
                _eventStreams.Add(context.Response.OutputStream);
            return;
        }

        if (path == "/api/workspaces")
        {
            List<WorkspaceDto> workspaces;
            lock (_lock)
                workspaces = _workspaces.ToList();
            await WriteJsonAsync(context, HttpStatusCode.OK, workspaces);
            return;
        }

        if (path.StartsWith("/api/workspaces/", StringComparison.Ordinal))
        {
            var id = Uri.UnescapeDataString(path["/api/workspaces/".Length..]);
            WorkspaceDto? workspace;
            lock (_lock)
                workspace = _workspaces.FirstOrDefault(w => w.Id == id);

            if (workspace is null)
                await WriteJsonAsync(context, HttpStatusCode.NotFound, Array.Empty<string>());
            else
                await WriteJsonAsync(context, HttpStatusCode.OK, workspace);
            return;
        }

        await WriteJsonAsync(context, HttpStatusCode.OK, Array.Empty<string>());
    }

    private async Task WriteJsonAsync(HttpListenerContext context, HttpStatusCode status, object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static int FindFreePort()
    {
        using var tcp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    public void Dispose()
    {
        try { _listener.Stop(); }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            TestContext.Progress.WriteLine(ex.Message);
        }

        lock (_lock)
        {
            foreach (var stream in _eventStreams)
            {
                try { stream.Dispose(); }
                catch (ObjectDisposedException ex)
                {
                    TestContext.Progress.WriteLine(ex.Message);
                }
            }

            _eventStreams.Clear();
        }
    }
}
