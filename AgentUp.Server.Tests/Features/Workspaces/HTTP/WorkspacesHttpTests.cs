using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
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

    [SetUp]
    public async Task SetUp()
    {
        var port = FindFreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [$"--urls=http://localhost:{port}"]
        });
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddSingleton<IWorkspaceRegistry>(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRegistry>());
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.MapWorkspaces();

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

        var body = await response.Content.ReadFromJsonAsync<List<Workspace>>();
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

        var workspace = await response.Content.ReadFromJsonAsync<Workspace>();
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
        var workspace = await response.Content.ReadFromJsonAsync<Workspace>();

        Assert.That(response.Headers.Location!.ToString(), Does.EndWith($"/api/workspaces/{workspace!.Id}"));
    }

    [Test]
    public async Task GetById_ReturnsWorkspace_AfterRegistration()
    {
        var request = new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1");
        var created = (await (await _client.PostAsJsonAsync("/api/workspaces", request)).Content.ReadFromJsonAsync<Workspace>())!;

        var response = await _client.GetAsync($"/api/workspaces/{created.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var workspace = await response.Content.ReadFromJsonAsync<Workspace>();
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
            new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"))).Content.ReadFromJsonAsync<Workspace>())!;

        var patchResponse = await _client.PatchAsJsonAsync(
            $"/api/workspaces/{created.Id}/state",
            new UpdateWorkspaceStateRequest(WorkspaceState.Running));

        Assert.That(patchResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var updated = await _client.GetFromJsonAsync<Workspace>($"/api/workspaces/{created.Id}");
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
            new RegisterWorkspaceRequest("A", "/r", "/r/a", "main", "c1"))).Content.ReadFromJsonAsync<Workspace>())!;

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
    public async Task MultipleWorkspaces_CoexistWithIsolatedState()
    {
        var a = (await (await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("Alpha", "/r", "/r/a", "feature/alpha", "aaa"))).Content.ReadFromJsonAsync<Workspace>())!;
        var b = (await (await _client.PostAsJsonAsync("/api/workspaces",
            new RegisterWorkspaceRequest("Beta", "/r", "/r/b", "feature/beta", "bbb"))).Content.ReadFromJsonAsync<Workspace>())!;

        Assert.That(a.Id, Is.Not.EqualTo(b.Id));

        await _client.PatchAsJsonAsync($"/api/workspaces/{a.Id}/state", new UpdateWorkspaceStateRequest(WorkspaceState.Running));

        var fetchedA = await _client.GetFromJsonAsync<Workspace>($"/api/workspaces/{a.Id}");
        var fetchedB = await _client.GetFromJsonAsync<Workspace>($"/api/workspaces/{b.Id}");

        Assert.That(fetchedA!.State, Is.EqualTo(WorkspaceState.Running));
        Assert.That(fetchedB!.State, Is.EqualTo(WorkspaceState.Stopped));
        Assert.That(fetchedA.Branch, Is.EqualTo("feature/alpha"));
        Assert.That(fetchedB.Branch, Is.EqualTo("feature/beta"));

        var all = await _client.GetFromJsonAsync<List<Workspace>>("/api/workspaces");
        Assert.That(all, Has.Count.EqualTo(2));
    }
}
