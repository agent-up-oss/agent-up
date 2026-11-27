using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class HostReviewQueueServiceTests
{
    [Test]
    public async Task SyncAsync_adoptsAWaitingPullOnTheWorkspaceRepository()
    {
        var host = await HostAsync(TeamMemberKind.DevReviewer, Pull("feature", "main", "aaa"));

        await host.Sync.SyncAsync(host.WorkspaceId, CancellationToken.None);

        var adopted = host.Queue.List().Single();
        Assert.Multiple(() =>
        {
            Assert.That(adopted.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(adopted.Proposal!.Branch, Is.EqualTo("feature"));
            Assert.That(adopted.Proposal.Head, Is.EqualTo("aaa"));
            Assert.That(host.Queue.AwaitingReview(host.WorkspaceId, TeamMemberKind.DevReviewer, "acme", "widgets"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task SyncAsync_doesNothingForAnAuthor()
    {
        var host = await HostAsync(TeamMemberKind.Author, Pull("feature", "main", "aaa"));

        await host.Sync.SyncAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(host.Queue.List(), Is.Empty);
    }

    [Test]
    public async Task SyncAsync_skipsAPullThatAlreadyHasThisKindOfReview()
    {
        var host = await HostAsync(
            TeamMemberKind.DevReviewer,
            new ForgePullRequest(
                "42",
                42,
                "Product cards",
                "https://example.test/42",
                [],
                [new ForgeReview("hector", "APPROVED", DateTimeOffset.UnixEpoch)],
                "feature",
                "main",
                "aaa"));

        await host.Sync.SyncAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(host.Queue.List(), Is.Empty);
    }

    [Test]
    public async Task SyncAsync_copiesACompletedOtherKindReviewOntoTheAdoptedTask()
    {
        var host = await HostAsync(
            TeamMemberKind.PmReviewer,
            new ForgePullRequest(
                "42",
                42,
                "Product cards",
                "https://example.test/42",
                ["lucy"],
                [new ForgeReview("hector", "APPROVED", DateTimeOffset.UnixEpoch)],
                "feature",
                "main",
                "aaa"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["hector"] = "devReviewer",
                ["lucy"] = "pmReviewer",
            },
            ["devReviewer", "pmReviewer"]);

        await host.Sync.SyncAsync(host.WorkspaceId, CancellationToken.None);

        var adopted = host.Queue.List().Single();
        Assert.Multiple(() =>
        {
            Assert.That(adopted.Reviews!.Single().Kind, Is.EqualTo(TeamMemberKind.DevReviewer));
            Assert.That(host.Queue.AwaitingReview(host.WorkspaceId, TeamMemberKind.PmReviewer, "acme", "widgets"), Has.Count.EqualTo(1));
            Assert.That(host.Queue.AwaitingReview(host.WorkspaceId, TeamMemberKind.DevReviewer, "acme", "widgets"), Is.Empty);
        });
    }

    [Test]
    public async Task SyncAsync_skipsAPullThatCannotNameItsBranches()
    {
        var host = await HostAsync(TeamMemberKind.DevReviewer, Pull(null, "main", "aaa"));

        await host.Sync.SyncAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(host.Queue.List(), Is.Empty);
    }

    private static ForgePullRequest Pull(string? head, string? @base, string? sha) =>
        new("42", 42, "Product cards", "https://example.test/42", [], [], head, @base, sha, "Ship the cards.");

    private static async Task<Host> HostAsync(
        TeamMemberKind kind,
        ForgePullRequest pull,
        IReadOnlyDictionary<string, string>? kindByLogin = null,
        IReadOnlyCollection<string>? kinds = null)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(JsonAgentWorkspaceSettingsRepository.Empty(workspace.Id) with { Member = kind });
        var forge = new StubForgeCapability { PullRequests = [pull] };
        var packages = new FakeEnabledCapabilityPackages().WithForges(forge);
        var credentials = new MemoryTaskSourceCredentialStore();
        var source = new TaskSourceService(
            credentials,
            packages,
            new FixedTeam(
                kindByLogin ?? new Dictionary<string, string> { ["hector"] = "devReviewer" },
                kinds ?? ["devReviewer"]),
            new FixedTaskSourceIdentities(new TaskSourcePerson(workspace.Id, "Hector")));
        await source.SaveAsync(
            new SaveTaskSourceTokenRequest(workspace.Id, "github", "https://github.com", "token"),
            CancellationToken.None);
        var queue = new TaskQueueService(new MemoryTaskQueueStore());
        var remotes = new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git"));
        var forgeController = new ForgeController(new ForgeService(
            workspaces,
            packages,
            new MemoryForgeCredentialStore(),
            remotes,
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("token")));
        return new Host(
            workspace.Id,
            queue,
            new HostReviewQueueService(queue, new TaskSourceController(source), forgeController, settings));
    }

    private sealed record Host(string WorkspaceId, TaskQueueService Queue, HostReviewQueueService Sync);

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }

    private sealed class FixedTeam(IReadOnlyDictionary<string, string> kindByLogin, IReadOnlyCollection<string> kinds)
        : ITaskSourceTeam
    {
        public Task<TaskSourceTeam> ForAsync(string owner, string repository, CancellationToken cancellationToken)
            => Task.FromResult(new TaskSourceTeam(kindByLogin, kinds));
    }
}
