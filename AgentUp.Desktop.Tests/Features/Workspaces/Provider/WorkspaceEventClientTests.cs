using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceEventClientTests
{
    [AvaloniaTest]
    public async Task WorkspaceEvents_debounceSupersededWorkspaceRefreshes()
    {
        using var server = new WorkspaceEventTestServer([WorkspaceFixtures.WithHttpPort("ws-1", 10000)]);
        using var http = new HttpClient { BaseAddress = new Uri(server.BaseUrl) };
        var sidebar = await CreateLoadedSidebarAsync(http);
        using var events = new WorkspaceEventClient(http, sidebar);

        events.Start();
        await server.WaitForEventSubscriberAsync();

        server.SetWorkspace(WorkspaceFixtures.WithHttpPort("ws-1", 10100));
        await server.EmitWorkspaceEventAsync("ws-1", "Running", [("App", "Running")]);
        server.SetWorkspace(WorkspaceFixtures.WithHttpPort("ws-1", 10200));
        await server.EmitWorkspaceEventAsync("ws-1", "Running", [("App", "Running")]);

        await WaitUntilAsync(() => server.WorkspaceGetCount("ws-1") == 1
            && sidebar.Workspaces.Single().Applications.Single().AllocatedPorts.Single().AllocatedPort == 10200);

        Assert.That(server.WorkspaceGetCount("ws-1"), Is.EqualTo(1));
        Assert.That(sidebar.ErrorMessage, Is.Null);
    }

    [AvaloniaTest]
    public async Task Stop_cancelsPendingWorkspaceRefresh()
    {
        using var server = new WorkspaceEventTestServer([WorkspaceFixtures.WithHttpPort("ws-1", 10000)]);
        using var http = new HttpClient { BaseAddress = new Uri(server.BaseUrl) };
        var sidebar = await CreateLoadedSidebarAsync(http);
        using var events = new WorkspaceEventClient(http, sidebar);

        events.Start();
        await server.WaitForEventSubscriberAsync();

        server.SetWorkspace(WorkspaceFixtures.WithHttpPort("ws-1", 10200));
        await server.EmitWorkspaceEventAsync("ws-1", "Running", [("App", "Running")]);
        events.Stop();
        await Task.Delay(400);

        Assert.That(server.WorkspaceGetCount("ws-1"), Is.Zero);
        Assert.That(sidebar.Workspaces.Single().Applications.Single().AllocatedPorts.Single().AllocatedPort, Is.EqualTo(10000));
    }

    [AvaloniaTest]
    public async Task WorkspaceEvents_removeDeletedWorkspaceFromSidebar()
    {
        using var server = new WorkspaceEventTestServer([WorkspaceFixtures.WithHttpPort("ws-1", 10000)]);
        using var http = new HttpClient { BaseAddress = new Uri(server.BaseUrl) };
        var sidebar = await CreateLoadedSidebarAsync(http);
        using var events = new WorkspaceEventClient(http, sidebar);

        events.Start();
        await server.WaitForEventSubscriberAsync();

        server.RemoveWorkspace("ws-1");
        await server.EmitWorkspaceEventAsync("ws-1", "Removed", []);

        await WaitUntilAsync(() => sidebar.Workspaces.Count == 0);

        Assert.That(sidebar.SelectedWorkspace, Is.Null);
    }

    private static async Task<WorkspaceListViewModel> CreateLoadedSidebarAsync(HttpClient http)
    {
        var api = new WorkspaceApiClient(http);
        var sidebar = new WorkspaceListViewModel(new WorkspacesController(new WorkspaceListService(api)));
        await Dispatcher.UIThread.InvokeAsync(async () => await sidebar.LoadAsync());
        return sidebar;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { });
            if (condition())
                return;

            await Task.Delay(50);
        }

        Assert.Fail("Expected condition was not observed before timeout.");
    }

    private sealed class WorkspaceEventTestServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Lock _lock = new();
        private readonly List<Stream> _eventStreams = [];
        private readonly Dictionary<string, int> _workspaceGetCounts = [];
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private List<WorkspaceDto> _workspaces;

        public string BaseUrl { get; }

        public WorkspaceEventTestServer(List<WorkspaceDto> workspaces)
        {
            _workspaces = workspaces;
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

        public void RemoveWorkspace(string workspaceId)
        {
            lock (_lock)
                _workspaces = _workspaces.Where(w => w.Id != workspaceId).ToList();
        }

        public int WorkspaceGetCount(string workspaceId)
        {
            lock (_lock)
                return _workspaceGetCounts.GetValueOrDefault(workspaceId);
        }

        public async Task EmitWorkspaceEventAsync(string workspaceId, string state, IReadOnlyList<(string Name, string State)> apps)
        {
            var applications = apps.Select(app => new { name = app.Name, state = app.State }).ToArray();
            var json = JsonSerializer.Serialize(new { workspaceId, state, applications });
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

        public async Task WaitForEventSubscriberAsync()
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                lock (_lock)
                {
                    if (_eventStreams.Count > 0)
                        return;
                }

                await Task.Delay(50);
            }

            Assert.Fail("WorkspaceEventClient did not connect to the event stream.");
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

            if (TryGetSingleWorkspaceId(path, out var id))
            {
                WorkspaceDto? workspace;
                lock (_lock)
                {
                    _workspaceGetCounts[id] = _workspaceGetCounts.GetValueOrDefault(id) + 1;
                    workspace = _workspaces.FirstOrDefault(w => w.Id == id);
                }

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

        private static bool TryGetSingleWorkspaceId(string path, out string id)
        {
            id = string.Empty;
            const string prefix = "/api/workspaces/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            var remaining = path[prefix.Length..];
            if (remaining.Length == 0 || remaining.Contains('/'))
                return false;

            id = Uri.UnescapeDataString(remaining);
            return true;
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
}
