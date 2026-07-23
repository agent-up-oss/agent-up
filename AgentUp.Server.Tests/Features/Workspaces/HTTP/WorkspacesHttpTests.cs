using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Services;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Repositories;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Repositories;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Tests.Features.Workspaces.HTTP;

[TestFixture]
public class WorkspacesHttpTests
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
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<IWorkspaceProcessManager, NullWorkspaceProcessManager>();
        builder.Services.AddSingleton<ProcessOutputService>();
        builder.Services.AddSingleton<ProcessesController>();
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<WorkspaceStateController>();
        builder.Services.AddSingleton<WorkspaceLifecycleService>();
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
    public async Task GetAll_ReturnsOk_WithEmptyList()
    {
        var response = await _client.GetAsync("/api/workspaces");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<Workspace>>(JsonOptions);
        Assert.That(body, Is.Empty);
    }

    [Test]
    public async Task Post_ReturnsCreated_WithAllFields()
    {
        var request = new RegisterWorkspaceRequest(
            DisplayName: "Agent 1",
            RepositoryPath: "/repos/app",
            WorktreePath: "/repos/app/.worktrees/agent-1",
            Branch: "feature/auth",
            Commit: "abc1234");

        var response = await _client.PostAsJsonAsync("/api/workspaces", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var workspace = await response.Content.ReadFromJsonAsync<Workspace>(JsonOptions);
        Assert.That(workspace, Is.Not.Null);
        Assert.That(workspace!.Id, Is.Not.Empty);
        Assert.That(workspace.DisplayName, Is.EqualTo("Agent 1"));
        Assert.That(workspace.RepositoryPath, Is.EqualTo("/repos/app"));
        Assert.That(workspace.WorktreePath, Is.EqualTo("/repos/app/.worktrees/agent-1"));
        Assert.That(workspace.Branch, Is.EqualTo("feature/auth"));
        Assert.That(workspace.Commit, Is.EqualTo("abc1234"));
        Assert.That(workspace.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public async Task Post_ReturnsLocationHeader_PointingToCreatedWorkspace()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1");

        var response = await _client.PostAsJsonAsync("/api/workspaces", request);
        var workspace = await response.Content.ReadFromJsonAsync<Workspace>(JsonOptions);

        Assert.That(response.Headers.Location!.ToString(), Does.EndWith($"/api/workspaces/{workspace!.Id}"));
    }

    [Test]
    public async Task GetById_ReturnsWorkspace_AfterRegistration()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1");
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var response = await _client.GetAsync($"/api/workspaces/{created.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var workspace = await response.Content.ReadFromJsonAsync<Workspace>(JsonOptions);
        Assert.That(workspace!.Id, Is.EqualTo(created.Id));
        Assert.That(workspace.DisplayName, Is.EqualTo("A"));
    }

    [Test]
    public async Task GetById_ReturnsNotFound_ForUnknownId()
    {
        var response = await _client.GetAsync("/api/workspaces/does-not-exist");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PatchState_ReturnsNoContent_AndUpdatesState()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"))).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var patchResponse = await _client.PatchAsJsonAsync(
            $"/api/workspaces/{created.Id}/state",
            new UpdateWorkspaceStateRequest(WorkspaceState.Running));

        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var updated = await _client.GetFromJsonAsync<Workspace>($"/api/workspaces/{created.Id}", JsonOptions);
        Assert.That(updated!.State, Is.EqualTo(WorkspaceState.Running));
    }

    [Test]
    public async Task PatchState_ReturnsNotFound_ForUnknownId()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/workspaces/ghost/state",
            new UpdateWorkspaceStateRequest(WorkspaceState.Running));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_ReturnsNoContent_AndRemovesWorkspace()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"))).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var deleteResponse = await _client.DeleteAsync($"/api/workspaces/{created.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await _client.GetAsync($"/api/workspaces/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_ReturnsNotFound_ForUnknownId()
    {
        var response = await _client.DeleteAsync("/api/workspaces/ghost");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task TutorialCleanup_RemovesAllWorkspaces()
    {
        await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("Tutorial 1", "/tmp/root/agent-up-tutorial/example-agent1", "/tmp/root/agent-up-tutorial/example-agent1", "not on a git branch", ""));
        await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("Normal", "/repos/app", "/repos/app", "main", "abc"));

        var response = await _client.PostAsync("/api/workspaces/tutorial/cleanup", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var workspaces = await _client.GetFromJsonAsync<List<Workspace>>("/api/workspaces", JsonOptions);
        Assert.That(workspaces, Is.Empty);
    }

    [Test]
    public async Task TutorialCleanup_RemovesWorkspace_WhenProcessKillFails()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.RegisterAsync(new RegisterWorkspaceRequest(
            "Normal",
            "/repos/app",
            "/repos/app",
            "main",
            ""));
        var processes = ServerTestComposition.CreateProcessesController(new KillFailingWorkspaceProcessManager());
        var lifecycle = new WorkspaceLifecycleService(registry, processes);
        var controller = new WorkspacesController(registry, lifecycle);

        await controller.CleanupTutorialWorkspaces();

        Assert.That(registry.GetAll(), Is.Empty);
    }

    [Test]
    public async Task PostStart_SetsStateToRunning()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"))).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var startResponse = await _client.PostAsync($"/api/workspaces/{created.Id}/start", null);

        Assert.That(startResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var workspace = await _client.GetFromJsonAsync<Workspace>($"/api/workspaces/{created.Id}", JsonOptions);
        Assert.That(workspace!.State, Is.EqualTo(WorkspaceState.Running));
    }

    [Test]
    public async Task PostStart_SetsApplicationStatesToRunning()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Applications =
            [
                new ApplicationDefinition("Frontend", "npm run dev", null, null),
                new ApplicationDefinition("Backend", "dotnet run", null, null)
            ]
        };
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        await _client.PostAsync($"/api/workspaces/{created.Id}/start", null);

        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);
        Assert.That(apps![0].State, Is.EqualTo(ApplicationState.Running));
        Assert.That(apps[1].State, Is.EqualTo(ApplicationState.Running));
    }

    [Test]
    public async Task PostStop_SetsApplicationStatesToStopped()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1")
        {
            Applications = [new ApplicationDefinition("Frontend", "npm run dev", null, null)]
        };
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;
        await _client.PostAsync($"/api/workspaces/{created.Id}/start", null);

        await _client.PostAsync($"/api/workspaces/{created.Id}/stop", null);

        var apps = await _client.GetFromJsonAsync<List<ApplicationInstance>>($"/api/workspaces/{created.Id}/applications", JsonOptions);
        Assert.That(apps![0].State, Is.EqualTo(ApplicationState.Stopped));
    }

    [Test]
    public async Task PostStart_ReturnsNotFound_ForUnknownId()
    {
        var response = await _client.PostAsync("/api/workspaces/ghost/start", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PostStart_SetsStateToFailed_AndReturnsProblem_WhenLaunchThrows()
    {
        var port = FindFreePort();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [$"--urls=http://localhost:{port}"] });
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
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<IWorkspaceProcessManager, FailingWorkspaceProcessManager>();
        builder.Services.AddSingleton<ProcessOutputService>();
        builder.Services.AddSingleton<ProcessesController>();
        builder.Services.AddSingleton<WorkspaceQueryController>();
        builder.Services.AddSingleton<WorkspaceStateController>();
        builder.Services.AddSingleton<WorkspaceLifecycleService>();
        builder.Services.AddSingleton<ApplicationLifecycleService>();
        builder.Logging.SetMinimumLevel(LogLevel.None);
        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };

        var created = (await (await client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"))).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        var startResponse = await client.PostAsync($"/api/workspaces/{created.Id}/start", null);

        Assert.That(startResponse.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));

        var body = await startResponse.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("Workspace could not be started."));
        Assert.That(body, Does.Not.Contain("No such file or directory"));

        var workspace = await client.GetFromJsonAsync<Workspace>($"/api/workspaces/{created.Id}", JsonOptions);
        Assert.That(workspace!.State, Is.EqualTo(WorkspaceState.Failed));

        await app.StopAsync();
        await app.DisposeAsync();
    }

    [Test]
    public async Task PostStop_SetsStateToStopped_AfterStart()
    {
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"))).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;
        await _client.PostAsync($"/api/workspaces/{created.Id}/start", null);

        var stopResponse = await _client.PostAsync($"/api/workspaces/{created.Id}/stop", null);

        Assert.That(stopResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var workspace = await _client.GetFromJsonAsync<Workspace>($"/api/workspaces/{created.Id}", JsonOptions);
        Assert.That(workspace!.State, Is.EqualTo(WorkspaceState.Stopped));
    }

    [Test]
    public async Task PostStop_ReturnsNotFound_ForUnknownId()
    {
        var response = await _client.PostAsync("/api/workspaces/ghost/stop", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task MultipleWorkspaces_CoexistWithIsolatedState()
    {
        var a = (await (await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("Alpha", "/r", "/r/a", "feature/alpha", "aaa"))).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;
        var b = (await (await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("Beta", "/r", "/r/b", "feature/beta", "bbb"))).Content.ReadFromJsonAsync<Workspace>(JsonOptions))!;

        Assert.That(a.Id, Is.Not.EqualTo(b.Id));

        await _client.PatchAsJsonAsync($"/api/workspaces/{a.Id}/state", new UpdateWorkspaceStateRequest(WorkspaceState.Running));

        var fetchedA = await _client.GetFromJsonAsync<Workspace>($"/api/workspaces/{a.Id}", JsonOptions);
        var fetchedB = await _client.GetFromJsonAsync<Workspace>($"/api/workspaces/{b.Id}", JsonOptions);

        Assert.That(fetchedA!.State, Is.EqualTo(WorkspaceState.Running));
        Assert.That(fetchedB!.State, Is.EqualTo(WorkspaceState.Stopped));
        Assert.That(fetchedA.Branch, Is.EqualTo("feature/alpha"));
        Assert.That(fetchedB.Branch, Is.EqualTo("feature/beta"));

        var all = await _client.GetFromJsonAsync<List<Workspace>>("/api/workspaces", JsonOptions);
        Assert.That(all, Has.Count.EqualTo(2));
    }

    private sealed class KillFailingWorkspaceProcessManager : IWorkspaceProcessManager
    {
        public Task LaunchAsync(Workspace workspace) => Task.CompletedTask;

        public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;

        public Task KillAsync(string workspaceId) =>
            throw new InvalidOperationException("Process is already gone");

        public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
    }
}
