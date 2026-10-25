using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Controller;

[TestFixture]
public sealed class TaskQueueControllerTests
{
    [Test]
    public async Task EnqueueAsync_picksAnOpenIssueOntoTheQueue()
    {
        var (controller, _, workspaceId) = await ArrangeAsync(Issue("7", 3));

        var queued = await controller.EnqueueAsync(new EnqueueTaskRequest(workspaceId, "7"));

        Assert.Multiple(() =>
        {
            Assert.That(queued!.Number, Is.EqualTo(3));
            Assert.That(queued.Owner, Is.EqualTo("acme"));
            Assert.That(queued.Repository, Is.EqualTo("widgets"));
            Assert.That(queued.HeldByWorkspaceId, Is.Null);
        });
    }

    [Test]
    public async Task EnqueueAsync_refusesAnIssueTheHostDoesNotServe()
    {
        var (controller, _, workspaceId) = await ArrangeAsync(Issue("7", 3));

        Assert.That(await controller.EnqueueAsync(new EnqueueTaskRequest(workspaceId, "nope")), Is.Null);
    }

    [Test]
    public async Task EnqueueAsync_refusesAWorkspaceThatIsNotThere()
    {
        var (controller, _, _) = await ArrangeAsync(Issue("7", 3));

        Assert.That(await controller.EnqueueAsync(new EnqueueTaskRequest("missing", "7")), Is.Null);
    }

    [Test]
    public async Task EnqueueAsync_carriesTheTargetWorkspaceThrough()
    {
        var (controller, _, workspaceId) = await ArrangeAsync(Issue("7", 3));

        var queued = await controller.EnqueueAsync(new EnqueueTaskRequest(workspaceId, "7", workspaceId));

        Assert.That(queued!.TargetWorkspaceId, Is.EqualTo(workspaceId));
    }

    [Test]
    public async Task Remove_leavesATaskAWorkspaceIsCarrying()
    {
        var (controller, tasks, workspaceId) = await ArrangeAsync(Issue("7", 3));
        await controller.EnqueueAsync(new EnqueueTaskRequest(workspaceId, "7"));
        tasks.TryClaim(workspaceId, "acme", "widgets", "7");

        Assert.Multiple(() =>
        {
            Assert.That(controller.Remove("acme", "widgets", "7"), Is.False);
            Assert.That(controller.List(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Release_freesAHeldTaskForSomebodyElse()
    {
        var (controller, tasks, workspaceId) = await ArrangeAsync(Issue("7", 3));
        await controller.EnqueueAsync(new EnqueueTaskRequest(workspaceId, "7"));
        tasks.TryClaim(workspaceId, "acme", "widgets", "7");

        var released = controller.Release("acme", "widgets", "7");

        Assert.Multiple(() =>
        {
            Assert.That(released, Is.True);
            Assert.That(controller.List().Single().HeldByWorkspaceId, Is.Null);
        });
    }

    [Test]
    public async Task Move_andTarget_reportWhenThereIsNoSuchTask()
    {
        var (controller, _, _) = await ArrangeAsync(Issue("7", 3));

        Assert.Multiple(() =>
        {
            Assert.That(controller.Move("acme", "widgets", "7", 0), Is.False);
            Assert.That(controller.Target("acme", "widgets", "7", null), Is.False);
        });
    }

    private static ForgeIssue Issue(string id, int number) =>
        new(id, number, $"Issue {number}", "body", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, $"https://example.test/{number}");

    private static async Task<(TaskQueueController Controller, TaskQueueService Tasks, string WorkspaceId)> ArrangeAsync(
        params ForgeIssue[] issues)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var credentials = new MemoryForgeCredentialStore();
        credentials.Write(workspace.Id, "github", "token");
        var service = new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability { Issues = issues }),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("token"));
        var tasks = new TaskQueueService(new MemoryTaskQueueStore());
        var forge = new ForgeController(service);
        return (new TaskQueueController(tasks, new TaskPickingService(tasks, forge)), tasks, workspace.Id);
    }

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }
}
