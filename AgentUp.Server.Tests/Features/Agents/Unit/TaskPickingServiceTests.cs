using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class TaskPickingServiceTests
{
    [Test]
    public async Task PickAsync_takesAnIssueFromTheStoredTokenWhenNoWorkspaceIsNamed()
    {
        var (picking, queue) = Arrange(Issue("I-57", 57));
        await picking.PickAsync(ServerDomain.EnqueueTask().Issue("I-57").FromRepository("github", "acme", "widgets").Build());

        Assert.That(queue.List().Single().Number, Is.EqualTo(57));
        Assert.That(queue.List().Single().Owner, Is.EqualTo("acme"));
    }

    [Test]
    public async Task PickAsync_returnsNothingWhenTheRequestDoesNotNameTheRepository()
    {
        var (picking, queue) = Arrange(Issue("I-57", 57));

        var queued = await picking.PickAsync(ServerDomain.EnqueueTask().Issue("I-57").Build());

        Assert.That(queued, Is.Null);
        Assert.That(queue.List(), Is.Empty);
    }

    [Test]
    public async Task PickAsync_returnsNothingWhenThatRepositoryHasNoSuchIssue()
    {
        var (picking, _) = Arrange(Issue("I-57", 57));

        var queued = await picking.PickAsync(ServerDomain.EnqueueTask().Issue("missing").FromRepository("github", "acme", "widgets").Build());

        Assert.That(queued, Is.Null);
    }

    private static ForgeIssue Issue(string id, int number) =>
        new(id, number, $"Issue {number}", "body", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, $"https://example.test/{number}");

    private static (TaskPickingService Picking, TaskQueueService Queue) Arrange(params ForgeIssue[] issues)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var credentials = new MemoryForgeCredentialStore();
        var forge = new ForgeController(new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability { Issues = issues }),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("token")));
        var sourceCredentials = new MemoryTaskSourceCredentialStore();
        sourceCredentials.Write("github", new TaskSourceCredential("token", "https://github.com"));
        var source = new TaskSourceController(new TaskSourceService(
            sourceCredentials,
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability { Issues = issues }),
            new EmptyTaskSourceTeam()));
        var queue = new TaskQueueService(new MemoryTaskQueueStore());
        return (new TaskPickingService(queue, forge, source), queue);
    }

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }
}
