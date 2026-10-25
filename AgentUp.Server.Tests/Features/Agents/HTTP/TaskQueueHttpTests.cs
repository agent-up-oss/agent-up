using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.Agents.HTTP;

[TestFixture]
public sealed class TaskQueueHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task GetTasks_isEmptyBeforeAnythingIsPicked()
    {
        await using var host = await HostAsync();

        var queue = await host.Client.GetFromJsonAsync<List<QueuedTaskDto>>("/api/tasks", Json);

        Assert.That(queue, Is.Empty);
    }

    [Test]
    public async Task GetForgeIssues_listsTheRepositorysOpenIssues()
    {
        await using var host = await HostAsync(Issue("I-57", 57));

        var issues = await host.Client.GetFromJsonAsync<List<ForgeIssueDto>>(
            $"/api/workspaces/{host.WorkspaceId}/forge/issues", Json);

        Assert.That(issues!.Single().Number, Is.EqualTo(57));
    }

    [Test]
    public async Task PostTasks_picksAnIssueOntoTheQueue()
    {
        await using var host = await HostAsync(Issue("I-57", 57));

        var response = await host.Client.PostAsJsonAsync(
            "/api/tasks",
            new EnqueueTaskRequest(host.WorkspaceId, "I-57"),
            Json);
        var queued = await response.Content.ReadFromJsonAsync<QueuedTaskDto>(Json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(queued!.Number, Is.EqualTo(57));
            Assert.That(queued.Owner, Is.EqualTo("acme"));
        });
    }

    [Test]
    public async Task PostTasks_refusesAnIssueTheHostDoesNotServe()
    {
        await using var host = await HostAsync(Issue("I-57", 57));

        var response = await host.Client.PostAsJsonAsync(
            "/api/tasks",
            new EnqueueTaskRequest(host.WorkspaceId, "nope"),
            Json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteTask_takesAFreeTaskBackOutOfTheQueue()
    {
        await using var host = await HostAsync(Issue("I-57", 57));
        await host.Client.PostAsJsonAsync("/api/tasks", new EnqueueTaskRequest(host.WorkspaceId, "I-57"), Json);

        var response = await host.Client.DeleteAsync("/api/tasks/acme/widgets/I-57");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(host.Queue.List(), Is.Empty);
        });
    }

    [Test]
    public async Task DeleteTask_refusesATaskAWorkspaceIsCarrying()
    {
        await using var host = await HostAsync(Issue("I-57", 57));
        await host.Client.PostAsJsonAsync("/api/tasks", new EnqueueTaskRequest(host.WorkspaceId, "I-57"), Json);
        host.Queue.TryClaim(host.WorkspaceId, "acme", "widgets", "I-57");

        var response = await host.Client.DeleteAsync("/api/tasks/acme/widgets/I-57");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task PutPosition_andPutTarget_arrangeTheQueue()
    {
        await using var host = await HostAsync(Issue("I-57", 57), Issue("I-58", 58));
        await host.Client.PostAsJsonAsync("/api/tasks", new EnqueueTaskRequest(host.WorkspaceId, "I-57"), Json);
        await host.Client.PostAsJsonAsync("/api/tasks", new EnqueueTaskRequest(host.WorkspaceId, "I-58"), Json);

        var moved = await host.Client.PutAsJsonAsync("/api/tasks/acme/widgets/I-58/position", new MoveTaskRequest(0), Json);
        var targeted = await host.Client.PutAsJsonAsync(
            "/api/tasks/acme/widgets/I-58/target",
            new TargetTaskRequest(host.WorkspaceId),
            Json);

        Assert.Multiple(() =>
        {
            Assert.That(moved.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(targeted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(host.Queue.List().Select(task => task.Number), Is.EqualTo(new[] { 58, 57 }));
            Assert.That(host.Queue.List()[0].TargetWorkspaceId, Is.EqualTo(host.WorkspaceId));
        });
    }

    [Test]
    public async Task PostRelease_handsAHeldTaskBack()
    {
        await using var host = await HostAsync(Issue("I-57", 57));
        await host.Client.PostAsJsonAsync("/api/tasks", new EnqueueTaskRequest(host.WorkspaceId, "I-57"), Json);
        host.Queue.TryClaim(host.WorkspaceId, "acme", "widgets", "I-57");

        var response = await host.Client.PostAsync("/api/tasks/acme/widgets/I-57/release", null);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(host.Queue.List().Single().HeldByWorkspaceId, Is.Null);
        });
    }

    [Test]
    public async Task ArrangingATaskThatIsNotQueuedReportsNotFound()
    {
        await using var host = await HostAsync();

        var moved = await host.Client.PutAsJsonAsync("/api/tasks/acme/widgets/nope/position", new MoveTaskRequest(0), Json);
        var released = await host.Client.PostAsync("/api/tasks/acme/widgets/nope/release", null);

        Assert.Multiple(() =>
        {
            Assert.That(moved.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(released.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    private static ForgeIssue Issue(string id, int number) =>
        new(id, number, $"Issue {number}", "body", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, $"https://example.test/{number}");

    private static async Task<Host> HostAsync(params ForgeIssue[] issues)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = ["--urls=http://127.0.0.1:0"] });
        builder.Services.AddControllers().AddApplicationPart(typeof(TaskQueueHttpController).Assembly);
        builder.Services.AddSingleton<IWorkspaceRepository, InMemoryWorkspaceRepository>();
        builder.Services.AddSingleton<IPortAllocationService, InMemoryPortAllocationService>();
        builder.Services.AddSingleton<PortsController>();
        builder.Services.AddSingleton(_ => new CapabilityReconciliationService());
        builder.Services.AddSingleton<CapabilitiesController>();
        builder.Services.AddSingleton<WorkspaceEventBus>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<WorkspaceRegistry>());
        builder.Services.AddSingleton<WorkspaceQueryController>();

        var credentials = new MemoryForgeCredentialStore();
        builder.Services.AddSingleton<IForgeCredentialStore>(credentials);
        builder.Services.AddSingleton<IEnabledCapabilityPackages>(
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability { Issues = issues }));
        builder.Services.AddSingleton<IForgeGitRemoteResolver>(
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git")));
        builder.Services.AddSingleton<IForgeOAuthSettings>(new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")));
        builder.Services.AddSingleton<IForgeOAuthStateStore, ForgeOAuthStateStore>();
        builder.Services.AddSingleton<IForgeOAuthExchange>(new FixedForgeOAuthExchange("token"));
        builder.Services.AddSingleton<ForgeService>();
        builder.Services.AddSingleton<ForgeController>();
        builder.Services.AddSingleton<ITaskQueueStore, MemoryTaskQueueStore>();
        builder.Services.AddSingleton<TaskQueueService>();
        builder.Services.AddSingleton<TaskPickingService>();
        builder.Services.AddSingleton<TaskQueueController>();

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        var workspaces = app.Services.GetRequiredService<WorkspaceQueryController>();
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        credentials.Write(workspace.Id, "github", "token");

        var address = app.Urls.First();
        return new Host(
            app,
            new HttpClient { BaseAddress = new Uri(address) },
            workspace.Id,
            app.Services.GetRequiredService<TaskQueueService>());
    }

    private sealed record Host(WebApplication App, HttpClient Client, string WorkspaceId, TaskQueueService Queue)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }
}
