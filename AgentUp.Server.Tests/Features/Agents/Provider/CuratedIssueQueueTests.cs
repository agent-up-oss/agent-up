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

    [Test]
    public async Task ListAssignedAsync_offersAnAuthorNothingWhileItsTaskIsInReview()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync();
        tasks.Enqueue(Task("1", 1));
        tasks.Enqueue(Task("2", 2));
        tasks.TryClaim(workspaceId, "acme", "widgets", "1");
        tasks.Begin(workspaceId, "acme", "widgets", "1", "main");
        tasks.Propose(workspaceId, "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        // The author waits on its reviewers rather than starting the next task with this one open.
        Assert.That(listed, Is.Empty);
    }

    [Test]
    public async Task ListAwaitingAsync_offersAReviewerTheTasksWhoseRevisionItHasNotReviewed()
    {
        var (_, tasks, workspaceId) = await ArrangeAsync();
        var proposals = new CuratedProposalQueue(tasks, Forge(workspaceId), Reviewer(workspaceId, TeamMemberKind.DevReviewer));
        tasks.Enqueue(Task("1", 1));
        tasks.TryClaim("bob", "acme", "widgets", "1");
        tasks.Begin("bob", "acme", "widgets", "1", "main");
        tasks.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");

        var before = await proposals.ListAwaitingAsync(workspaceId, CancellationToken.None);
        tasks.Review("acme", "widgets", "1", new TaskReview(workspaceId, TaskReviewVerdict.Approve, 1, "ok", TeamMemberKind.DevReviewer), [TeamMemberKind.DevReviewer, TeamMemberKind.PmReviewer]);
        var after = await proposals.ListAwaitingAsync(workspaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(before.Single(), Is.EqualTo(new AssignedProposal("1", "Task 1", "", DateTimeOffset.UnixEpoch, "https://example.test/pulls/7", 1)));
            Assert.That(after, Is.Empty);
        });
    }

    // A dev reviewer that took a review keeps it; another dev reviewer is not offered it again.
    [Test]
    public async Task TryTakeAsync_givesEachReviewOfAKindToOneReviewer()
    {
        var (_, tasks, workspaceId) = await ArrangeAsync();
        var proposals = new CuratedProposalQueue(tasks, Forge(workspaceId), Reviewer(workspaceId, TeamMemberKind.PmReviewer));
        Proposed(tasks);
        tasks.TryClaimReview("lucy", TeamMemberKind.PmReviewer, "acme", "widgets", "1");

        var taken = await proposals.TryTakeAsync(workspaceId, "1", CancellationToken.None);
        var offered = await proposals.ListAwaitingAsync(workspaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.False);
            Assert.That(offered, Is.Empty);
        });
    }

    // A failing check on the revision under review is feedback, so the author is called again.
    [Test]
    public async Task ListAssignedAsync_handsATaskBackToItsAuthorWhenTheHostsChecksFailOnIt()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync(new ForgeHostSnapshot(
            new ForgeProposal("1", "7", "Task 1", "https://example.test/pulls/7", "agent-up/issue-1", "main", "open", "aaa"),
            [new ForgeCheck("test", "completed", "failure", "https://example.test/runs/3"), new ForgeCheck("lint", "completed", "success", "")],
            [],
            null));
        tasks.Enqueue(Task("1", 1));
        tasks.TryClaim(workspaceId, "acme", "widgets", "1");
        tasks.Begin(workspaceId, "acme", "widgets", "1", "main");
        tasks.Propose(workspaceId, "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main", "aaa");

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(listed.Single().Id, Is.EqualTo("1"));
            Assert.That(tasks.Get("acme", "widgets", "1")!.Stage, Is.EqualTo(TaskStage.ChangesRequested));
        });
    }

    // Checks on a head the revision is not, such as the one before the last push, decide nothing.
    [Test]
    public async Task ListAssignedAsync_keepsWaitingWhenTheFailingChecksAreOnAnotherCommit()
    {
        var (queue, tasks, workspaceId) = await ArrangeAsync(new ForgeHostSnapshot(
            new ForgeProposal("1", "7", "Task 1", "https://example.test/pulls/7", "agent-up/issue-1", "main", "open", "old"),
            [new ForgeCheck("test", "completed", "failure", "")],
            [],
            null));
        tasks.Enqueue(Task("1", 1));
        tasks.TryClaim(workspaceId, "acme", "widgets", "1");
        tasks.Begin(workspaceId, "acme", "widgets", "1", "main");
        tasks.Propose(workspaceId, "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main", "aaa");

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.NumberAsc, CancellationToken.None);

        Assert.That(listed, Is.Empty);
    }

    private static void Proposed(TaskQueueService tasks)
    {
        tasks.Enqueue(Task("1", 1));
        tasks.TryClaim("bob", "acme", "widgets", "1");
        tasks.Begin("bob", "acme", "widgets", "1", "main");
        tasks.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");
    }

    private static InMemoryAgentWorkspaceSettingsRepository Reviewer(string workspaceId, TeamMemberKind kind)
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(settings.Get(workspaceId) with { Member = kind });
        return settings;
    }

    private static QueuedTask Task(string id, int number) =>
        new(id, "acme", "widgets", number, $"Task {number}", "", $"https://example.test/{number}", DateTimeOffset.UnixEpoch);

    private static async Task<(CuratedIssueQueue Queue, TaskQueueService Tasks, string WorkspaceId)> ArrangeAsync(ForgeHostSnapshot? snapshot = null)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var credentials = new MemoryForgeCredentialStore();
        credentials.Write(workspace.Id, "github", "token");
        var service = new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability { Snapshot = snapshot ?? new ForgeHostSnapshot(null, [], [], null) }),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("token"));
        var tasks = new TaskQueueService(new MemoryTaskQueueStore());
        Forges[workspace.Id] = new ForgeController(service);
        return (new CuratedIssueQueue(tasks, Forges[workspace.Id]), tasks, workspace.Id);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ForgeController> Forges = new();

    private static ForgeController Forge(string workspaceId) => Forges[workspaceId];

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }
}
