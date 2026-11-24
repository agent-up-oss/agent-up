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

        var queued = await controller.EnqueueAsync(ServerDomain.EnqueueTask().FromWorkspace(workspaceId).Issue("7").Build());

        Assert.Multiple(() =>
        {
            Assert.That(queued!.Number, Is.EqualTo(3));
            Assert.That(queued.Owner, Is.EqualTo("acme"));
            Assert.That(queued.Repository, Is.EqualTo("widgets"));
            Assert.That(queued.HeldByWorkspaceId, Is.Null);
        });
    }

    // A reader can tell which commit is under review, not only which revision number.
    [Test]
    public async Task List_namesTheCommitEachProposalRevisionIs()
    {
        var (controller, tasks, workspaceId) = await ArrangeAsync(Issue("7", 3));
        await controller.EnqueueAsync(ServerDomain.EnqueueTask().FromWorkspace(workspaceId).Issue("7").Build());
        tasks.TryClaim(workspaceId, "acme", "widgets", "7");
        tasks.Begin(workspaceId, "acme", "widgets", "7", "main");
        tasks.Propose(workspaceId, "acme", "widgets", "7", "9", "https://example.test/pulls/9", "agent-up/issue-3", "main", "abc123");

        var proposal = controller.List().Single().Proposal;

        Assert.That(proposal, Is.EqualTo(new TaskProposalDto("9", "https://example.test/pulls/9", "agent-up/issue-3", "main", 1, "abc123")));
    }

    // The Tasks overview reads which kinds of review a revision still waits on, and who holds each.
    [Test]
    public async Task List_namesWhoHoldsEachReviewAndWhichKindsTheRevisionStillNeeds()
    {
        var (controller, tasks, workspaceId) = await ArrangeAsync(TeamMemberKind.DevReviewer, Issue("7", 3));
        await controller.EnqueueAsync(ServerDomain.EnqueueTask().FromWorkspace(workspaceId).Issue("7").Build());
        tasks.TryClaim("bob", "acme", "widgets", "7");
        tasks.Begin("bob", "acme", "widgets", "7", "main");
        tasks.Propose("bob", "acme", "widgets", "7", "9", "https://example.test/pulls/9", "agent-up/issue-3", "main", "abc123");
        tasks.TryClaimReview(workspaceId, TeamMemberKind.DevReviewer, "acme", "widgets", "7");

        var before = controller.List().Single();
        tasks.Review("acme", "widgets", "7", new TaskReview(workspaceId, TaskReviewVerdict.Approve, 1, "ok", TeamMemberKind.DevReviewer), [TeamMemberKind.DevReviewer]);
        var after = controller.List().Single();

        Assert.Multiple(() =>
        {
            Assert.That(before.DevReviewerWorkspaceId, Is.EqualTo(workspaceId));
            Assert.That(before.NeedsReview, Is.EqualTo(new[] { "devReviewer" }));
            Assert.That(after.NeedsReview, Is.Empty);
            Assert.That(after.Reviews!.Single().Kind, Is.EqualTo("devReviewer"));
        });
    }

    [Test]
    public async Task EnqueueAsync_refusesAnIssueTheHostDoesNotServe()
    {
        var (controller, _, workspaceId) = await ArrangeAsync(Issue("7", 3));

        Assert.That(await controller.EnqueueAsync(ServerDomain.EnqueueTask().FromWorkspace(workspaceId).Issue("nope").Build()), Is.Null);
    }

    [Test]
    public async Task EnqueueAsync_refusesAWorkspaceThatIsNotThere()
    {
        var (controller, _, _) = await ArrangeAsync(Issue("7", 3));

        Assert.That(await controller.EnqueueAsync(ServerDomain.EnqueueTask().FromWorkspace("missing").Issue("7").Build()), Is.Null);
    }

    [Test]
    public async Task EnqueueAsync_carriesTheTargetWorkspaceThrough()
    {
        var (controller, _, workspaceId) = await ArrangeAsync(Issue("7", 3));

        var queued = await controller.EnqueueAsync(ServerDomain.EnqueueTask().FromWorkspace(workspaceId).Issue("7").Targeting(workspaceId).Build());

        Assert.That(queued!.TargetWorkspaceId, Is.EqualTo(workspaceId));
    }

    [Test]
    public async Task Remove_leavesATaskAWorkspaceIsCarrying()
    {
        var (controller, tasks, workspaceId) = await ArrangeAsync(Issue("7", 3));
        await controller.EnqueueAsync(ServerDomain.EnqueueTask().FromWorkspace(workspaceId).Issue("7").Build());
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
        await controller.EnqueueAsync(ServerDomain.EnqueueTask().FromWorkspace(workspaceId).Issue("7").Build());
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

    private static Task<(TaskQueueController Controller, TaskQueueService Tasks, string WorkspaceId)> ArrangeAsync(
        params ForgeIssue[] issues) => ArrangeAsync(TeamMemberKind.Author, issues);

    private static async Task<(TaskQueueController Controller, TaskQueueService Tasks, string WorkspaceId)> ArrangeAsync(
        TeamMemberKind member,
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
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(settings.Get(workspace.Id) with { Member = member });
        return (
            new TaskQueueController(
                tasks,
                new TaskPickingService(tasks, forge, TaskSourceComposition.Controller()),
                new TaskReviewRequirementService(forge, workspaces, settings)),
            tasks,
            workspace.Id);
    }

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }
}
