using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.DTOs;
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
using System.Net;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace AgentUp.Server.Tests.Features.Applications.HTTP;

[TestFixture]
public class ApplicationsHttpTests
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
        builder.Services.AddSingleton<AgentUp.Server.Features.Applications.Providers.AppMetricsHttpClient>();
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

    private static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }

    [Test]
    public async Task GetApplications_ReturnsEmpty_WhenNoApplicationsDefined()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            ServerDomain.Workspace().Build())).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.GetAsync($"/api/workspaces/{created.Id}/applications");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var apps = await response.Content.ReadFromJsonAsync<List<ApplicationInstance>>(JsonOptions);
        Assert.That(apps, Is.Empty);
    }

    [Test]
    public async Task GetApplications_ReturnsApplications_WithStoppedState()
    {
        var request = ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder("Frontend", ServerDomain.WebCommand)
                    .At("./frontend")
                    .WithPort(ServerDomain.Port().Named("WEB_PORT").On(3000))
                    .Build())
            .WithApplication(new ApplicationDefinitionBuilder("Backend", ServerDomain.ApiCommand).At("./api").Build())
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);

        Assert.That(apps, Has.Count.EqualTo(2));
        Assert.That(apps![0].Name, Is.EqualTo("Frontend"));
        Assert.That(apps[0].Command, Is.EqualTo("npm run dev"));
        Assert.That(apps[0].State, Is.EqualTo(ApplicationState.Stopped));
        Assert.That(apps[1].Name, Is.EqualTo("Backend"));
    }

    [Test]
    public async Task GetApplications_ReturnsNotFound_ForUnknownWorkspace()
    {
        var response = await _client.GetAsync("/api/workspaces/does-not-exist/applications");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Register_UpdatesApplications_OnReRegister()
    {
        var request = ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder("Frontend", ServerDomain.WebCommand).Build())
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var updated = ServerDomain.Workspace()
            .AtCommit(ServerDomain.SecondCommit)
            .WithApplication(new ApplicationDefinitionBuilder("Frontend", "npm run build").Build())
            .Build();
        var reregistered = (await (await _client.PostAsJsonAsync("/api/workspaces", updated)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        Assert.That(reregistered.Id, Is.EqualTo(created.Id));
        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);
        Assert.That(apps![0].Command, Is.EqualTo("npm run build"));
    }

    [Test]
    public async Task PostApplicationStart_SetsAppStateToRunning()
    {
        var request = ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder("Frontend", ServerDomain.WebCommand).Build())
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.PostAsync($"/api/workspaces/{created.Id}/applications/Frontend/start", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);
        Assert.That(apps![0].State, Is.EqualTo(ApplicationState.Running));
    }

    [Test]
    public async Task PostApplicationStart_PreparesADesktopApplication()
    {
        var request = ServerDomain.Workspace()
            .WithDesktopApplication(new DesktopApplicationDefinition("Editor", "dotnet run", "."))
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.PostAsync($"/api/workspaces/{created.Id}/applications/Editor/start", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);
        Assert.Multiple(() =>
        {
            Assert.That(apps![0].State, Is.EqualTo(ApplicationState.Running));
            Assert.That(apps[0].Kind, Is.EqualTo(ApplicationKind.Desktop));
        });
    }

    [Test]
    public async Task PostApplicationStop_SetsAppStateToStopped()
    {
        var request = ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder("Frontend", ServerDomain.WebCommand).Build())
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;
        await _client.PostAsync($"/api/workspaces/{created.Id}/applications/Frontend/start", null);

        var response = await _client.PostAsync($"/api/workspaces/{created.Id}/applications/Frontend/stop", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);
        Assert.That(apps![0].State, Is.EqualTo(ApplicationState.Stopped));
    }

    [Test]
    public async Task PostApplicationRestart_SetsAppStateToRunning()
    {
        var request = ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder("Frontend", ServerDomain.WebCommand).Build())
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.PostAsync($"/api/workspaces/{created.Id}/applications/Frontend/restart", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);
        Assert.That(apps![0].State, Is.EqualTo(ApplicationState.Running));
    }

    [Test]
    public async Task PostApplicationStart_ReturnsNotFound_ForUnknownApp()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            ServerDomain.Workspace().Build())).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.PostAsync($"/api/workspaces/{created.Id}/applications/ghost/start", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetApplicationMetrics_ReturnsNotFound_ForUnknownApp()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            ServerDomain.Workspace().Build())).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.GetAsync($"/api/workspaces/{created.Id}/applications/ghost/metrics");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetApplicationOutput_ReturnsEmptyList_BeforeAnyOutput()
    {
        var request = ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder("Frontend", ServerDomain.WebCommand).Build())
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.GetAsync($"/api/workspaces/{created.Id}/applications/Frontend/output");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var lines = await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions);
        Assert.That(lines, Is.Empty);
    }

    [Test]
    public async Task GetApplicationOutput_ReturnsNotFound_ForUnknownApp()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            ServerDomain.Workspace().Build())).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.GetAsync($"/api/workspaces/{created.Id}/applications/ghost/output");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Register_DockerServices_AppearInApplicationsList_WithDockerType()
    {
        var request = ServerDomain.Workspace()
            .WithService(new DockerServiceDefinition(
                    Name: "Database",
                    Image: "postgres:16",
                    Ports: [ServerDomain.Port().Named("DB_PORT").On(5432).Build()],
                    Environment: new Dictionary<string, string> { ["POSTGRES_PASSWORD"] = "not-a-real-value" },
                    Volumes: ["pgdata:/var/lib/postgresql/data"]))
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);

        Assert.That(apps, Has.Count.EqualTo(1));
        var db = apps![0];
        Assert.That(db.Name, Is.EqualTo("Database"));
        Assert.That(db.ServiceType, Is.EqualTo(ServiceType.Docker));
        Assert.That(db.Image, Is.EqualTo("postgres:16"));
        Assert.That(db.Ports, Has.Count.EqualTo(1));
        Assert.That(db.Ports[0].DefaultPort, Is.EqualTo(5432));
        Assert.That(db.AllocatedPorts, Has.Count.EqualTo(1));
        Assert.That(db.Environment!["POSTGRES_PASSWORD"], Is.EqualTo("not-a-real-value"));
        Assert.That(db.Volumes, Is.EqualTo(new[] { "pgdata:/var/lib/postgresql/data" }));
        Assert.That(db.State, Is.EqualTo(ApplicationState.Stopped));
    }

    [Test]
    public async Task Register_MixedApplicationsAndServices_BothAppearInList()
    {
        var request = ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder("API", ServerDomain.ApiCommand).Build())
            .WithService(new DockerServiceDefinition("Database", "postgres:16"))
            .Build();
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);

        Assert.That(apps!, Has.Count.EqualTo(2));
        Assert.That(apps!.Single(a => a.Name == "API").ServiceType, Is.EqualTo(ServiceType.Process));
        Assert.That(apps!.Single(a => a.Name == "Database").ServiceType, Is.EqualTo(ServiceType.Docker));
    }
}
