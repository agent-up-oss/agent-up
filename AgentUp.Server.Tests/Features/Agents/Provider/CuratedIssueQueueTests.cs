using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class CuratedIssueQueueTests
{
    [Test]
    public async Task ListAssignedAsync_offersNothingUntilSomebodyPicksAnIssue()
    {
        var (queue, _, workspaceId) = await ArrangeAsync();

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.That(listed, Is.Empty);
    }

    [Test]
    public async Task ListAssignedAsync_offersThePickedTasksInQueueOrderRatherThanBySort()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("9", 9));
        tasks.Enqueue(Task("2", 2));

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.That(listed.Select(issue => issue.Number), Is.EqualTo(new[] { 9, 2 }));
    }

    [Test]
    public async Task ListAssignedAsync_offersOnlyTheHeldTaskOnceTheWorkspaceIsCarryingOne()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("1", 1));
        tasks.Enqueue(Task("2", 2));
        tasks.TryClaim(workspaceId, "acme", "widgets", "2");

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.That(listed.Single().Number, Is.EqualTo(2));
    }

    [Test]
    public async Task ListAssignedAsync_hidesWhatAnotherWorkspaceIsCarrying()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("1", 1));
        tasks.TryClaim("somebody-else", "acme", "widgets", "1");

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.That(listed, Is.Empty);
    }

    [Test]
    public async Task ListAssignedAsync_hidesATaskPointedAtAnotherWorkspace()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("1", 1) with { TargetWorkspaceId = "somebody-else" });

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.That(listed, Is.Empty);
    }

    [Test]
    public async Task ListAssignedAsync_hidesTasksFromAnotherRepository()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("1", 1) with { Repository = "elsewhere" });

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.That(listed, Is.Empty);
    }

    [Test]
    public async Task TryHoldAsync_takesTheNamedTaskAndRefusesItToTheNext()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("1", 1));

        var held = await queue.TryHoldAsync(workspaceId, "1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(held, Is.True);
            Assert.That(tasks.Held(workspaceId)?.Id, Is.EqualTo("1"));
            Assert.That(tasks.TryClaim("somebody-else", "acme", "widgets", "1"), Is.False);
        });
    }

    [Test]
    public async Task CompleteAsync_retiresTheTaskSoNothingPicksItUpAgain()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("1", 1));
        await queue.TryHoldAsync(workspaceId, "1", CancellationToken.None);

        await queue.CompleteAsync(workspaceId, "1", CancellationToken.None);

        Assert.That(tasks.List(), Is.Empty);
    }

    [Test]
    public async Task Replace_leavesTheHoldAloneSoAnUnfinishedRunKeepsItsTask()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("1", 1));
        await queue.TryHoldAsync(workspaceId, "1", CancellationToken.None);

        queue.Replace(workspaceId, []);

        Assert.That(tasks.Held(workspaceId)?.Id, Is.EqualTo("1"));
    }

    private static QueuedTask Task(string id, int number) =>
        new(id, "acme", "widgets", number, $"Task {number}", "", $"https://example.test/{number}", DateTimeOffset.UnixEpoch);

    private static async Task<(CuratedIssueQueue Queue, TaskQueueService Tasks, string WorkspaceId)> ArrangeAsync()
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var credentials = new MemoryForgeCredentialStore();
        credentials.Write(workspace.Id, "github", "token");
        var service = new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability()),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("token"));
        var tasks = new TaskQueueService(new MemoryTaskQueueStore());
        return (new CuratedIssueQueue(tasks, new ForgeController(service)), tasks, workspace.Id);
    }

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }
}
