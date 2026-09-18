using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Providers;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.DesktopApplications.DTOs;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Ports.Services;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Net;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace AgentUp.Server.Tests.Features.DesktopApplications.HTTP;

[TestFixture]
public sealed class DesktopApplicationsHttpTests
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [SetUp]
    public async Task SetUp()
    {
        var port = FindFreePort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [$"--urls=http://localhost:{port}"]
        });
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(WorkspacesController).Assembly)
            .AddJsonOptions(opts =>
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IOutputRepository, InMemoryOutputRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton(_ => new CapabilityReconciliationService([]));
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<WorkspaceEventBus>();
        builder.Services.AddSingleton<AgentUp.Server.Features.Workspaces.Providers.WorkspaceEventFrameProvider>();
        builder.Services.AddSingleton<WorkspaceEventStreamService>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<IWorkspaceProcessManager, NullWorkspaceProcessManager>();
        builder.Services.AddSingleton<ProcessOutputService>();
        builder.Services.AddSingleton<ProcessesController>();
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<WorkspaceStateController>();
        builder.Services.AddSingleton<BrowserRemoteDisplayService>();
        builder.Services.AddSingleton<BrowserEventBus>();
        builder.Services.AddSingleton<IAuditEventRepository, InMemoryAuditEventRepository>();
        builder.Services.AddSingleton<IAuditArtifactRepository, InMemoryAuditArtifactRepository>();
        builder.Services.AddSingleton<IAuditIdentityProvider, FakeAuditIdentityProvider>();
        builder.Services.AddSingleton<AuditEventBus>();
        builder.Services.AddSingleton<AuditService>();
        builder.Services.AddSingleton<AuditController>();
        builder.Services.AddSingleton<AppHealthCheckService>();
        builder.Services.AddSingleton<AppHealthController>();
        builder.Services.AddSingleton<AppMetricsHttpClient>();
        builder.Services.AddSingleton<AppMetricsPullService>();
        builder.Services.AddSingleton<AppMetricsController>();
        builder.Services.AddSingleton<ApplicationMetricsService>();
        builder.Services.AddSingleton(sp => new WorkspaceStreamStateService(
            sp.GetRequiredService<BrowserEventBus>(),
            sp.GetRequiredService<AppHealthController>(),
            sp.GetRequiredService<WorkspaceQueryController>(),
            sp.GetRequiredService<AuditController>(),
            sp.GetRequiredService<ILogger<WorkspaceStreamStateService>>()));
        builder.Services.AddSingleton<WorkspaceStreamStateController>();
        builder.Services.AddSingleton(sp => new HeadlessBrowserSessionManager(
            Path.GetTempPath(), Path.GetTempPath(),
            sp.GetRequiredService<BrowserRemoteDisplayService>(),
            sp.GetRequiredService<WorkspaceStreamStateService>(),
            sp.GetRequiredService<ILogger<HeadlessBrowserSessionManager>>()));
        builder.Services.AddSingleton<BrowserLifecycleController>();
        builder.Services.AddWorkspaceLifecycleSupport();
        builder.Services.AddSingleton<ApplicationLifecycleService>();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.UseWebSockets();
        _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task Get_returnsNotFound_whenDesktopSessionIsMissing()
    {
        var response = await _client.GetAsync("/api/desktop-applications/missing/Editor");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ViewerTicket_andFramebufferRoutes_workAfterDesktopStart()
    {
        var created = await RegisterAndStartDesktopAsync();

        var sessionResponse = await _client.GetAsync($"/api/desktop-applications/{created.Id}/Editor");
        Assert.That(sessionResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var session = await sessionResponse.Content.ReadFromJsonAsync<DesktopSessionDto>(JsonOptions);
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.Generation, Is.GreaterThan(0));

        var ticketResponse = await _client.PostAsync($"/api/desktop-applications/{created.Id}/Editor/viewer-ticket", null);
        Assert.That(ticketResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ticket = await ticketResponse.Content.ReadFromJsonAsync<DesktopViewerTicketDto>(JsonOptions);
        Assert.That(ticket, Is.Not.Null);
        Assert.That(ticket!.ViewerUrl, Does.Contain(session.SessionId));

        var viewer = await _client.GetAsync(ticket!.ViewerUrl);
        Assert.That(viewer.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await viewer.Content.ReadAsStringAsync(), Does.Contain("pointerdown"));

        var screenshot = await _client.GetAsync($"/api/desktop-applications/{created.Id}/Editor/screenshot?generation={session.Generation}");
        Assert.That(screenshot.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(screenshot.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));

        var pointerDown = await _client.PostAsJsonAsync(
            $"/api/desktop-applications/{created.Id}/Editor/pointer-down",
            new DesktopPointerRequest(session.Generation, 10, 20, 1));
        var pointerUp = await _client.PostAsJsonAsync(
            $"/api/desktop-applications/{created.Id}/Editor/pointer-up",
            new DesktopPointerRequest(session.Generation, 10, 20, 1));
        var key = await _client.PostAsJsonAsync(
            $"/api/desktop-applications/{created.Id}/Editor/key",
            new DesktopKeyRequest(session.Generation, "Escape", true));
        Assert.That(pointerDown.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(pointerUp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(key.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var missingTicket = await _client.GetAsync($"/api/desktop-applications/session/{session.SessionId}/display");
        Assert.That(missingTicket.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var displayPath = ticket.ViewerUrl.Replace("/viewer?", "/display?", StringComparison.Ordinal);
        var notWebsocket = await _client.GetAsync(displayPath);
        Assert.That(notWebsocket.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new UriBuilder(new Uri(_client.BaseAddress!, displayPath)) { Scheme = "ws" }.Uri, CancellationToken.None);
        Assert.That(socket.State, Is.EqualTo(WebSocketState.Open));
        socket.Abort();
    }

    private async Task<Workspace> RegisterAndStartDesktopAsync()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            ServerDomain.Workspace()
                .WithDesktopApplication(new DesktopApplicationDefinition("Editor", "dotnet run", "."))
                .Build())).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;
        var start = await _client.PostAsync($"/api/workspaces/{created.Id}/applications/Editor/start", null);
        Assert.That(start.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        return created;
    }

    private static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
