using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Forge.Unit;

[TestFixture]
public sealed class ForgeAssignedIssueQueueTests
{
    [Test]
    public async Task ListAssignedAsync_ordersByCreatedWhenAsked()
    {
        var (queue, workspaceId) = await QueueAsync(
            new ForgeAssignedIssue("2", 2, "later", "", DateTimeOffset.Parse("2026-02-01Z"), DateTimeOffset.Parse("2026-02-02Z"), "u2"),
            new ForgeAssignedIssue("1", 1, "earlier", "", DateTimeOffset.Parse("2026-01-01Z"), DateTimeOffset.Parse("2026-03-01Z"), "u1"));

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.CreatedAsc, CancellationToken.None);

        Assert.That(listed.Select(issue => issue.Number), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task ListAssignedAsync_ordersByUpdatedWhenAsked()
    {
        var (queue, workspaceId) = await QueueAsync(
            new ForgeAssignedIssue("2", 2, "later-created", "", DateTimeOffset.Parse("2026-02-01Z"), DateTimeOffset.Parse("2026-02-02Z"), "u2"),
            new ForgeAssignedIssue("1", 1, "earlier-created", "", DateTimeOffset.Parse("2026-01-01Z"), DateTimeOffset.Parse("2026-03-01Z"), "u1"));

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.UpdatedAsc, CancellationToken.None);

        Assert.That(listed.Select(issue => issue.Number), Is.EqualTo(new[] { 2, 1 }));
    }

    [Test]
    public async Task ListAssignedAsync_ordersByNumberForAnUnknownOrder()
    {
        var (queue, workspaceId) = await QueueAsync(
            new ForgeAssignedIssue("2", 2, "two", "", DateTimeOffset.Parse("2026-01-01Z"), DateTimeOffset.Parse("2026-01-01Z"), "u2"),
            new ForgeAssignedIssue("1", 1, "one", "", DateTimeOffset.Parse("2026-02-01Z"), DateTimeOffset.Parse("2026-02-01Z"), "u1"));

        var listed = await queue.ListAssignedAsync(workspaceId, (AssignedIssueOrder)99, CancellationToken.None);

        Assert.That(listed.Select(issue => issue.Number), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task Replace_isANoOp()
    {
        var (queue, workspaceId) = await QueueAsync(
            new ForgeAssignedIssue("1", 1, "one", "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "u"));

        queue.Replace(workspaceId, []);

        var listed = await queue.ListAssignedAsync(workspaceId, AssignedIssueOrder.CreatedAsc, CancellationToken.None);
        Assert.That(listed.Single().Number, Is.EqualTo(1));
    }

    [Test]
    public async Task ListAssignedAsync_isEmptyWithoutACredential()
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var service = new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability
            {
                Assigned = [new ForgeAssignedIssue("1", 1, "one", "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "u")]
            }),
            new MemoryForgeCredentialStore(),
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("token"));
        var queue = new ForgeAssignedIssueQueue(new ForgeController(service));

        var listed = await queue.ListAssignedAsync(workspace.Id, AssignedIssueOrder.CreatedAsc, CancellationToken.None);

        Assert.That(listed, Is.Empty);
    }

    private static async Task<(ForgeAssignedIssueQueue Queue, string WorkspaceId)> QueueAsync(params ForgeAssignedIssue[] issues)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var credentials = new MemoryForgeCredentialStore();
        credentials.Write(workspace.Id, "github", "token");
        var service = new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability { Assigned = issues }),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("token"));
        return (new ForgeAssignedIssueQueue(new ForgeController(service)), workspace.Id);
    }
}
