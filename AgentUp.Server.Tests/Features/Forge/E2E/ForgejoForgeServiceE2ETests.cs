using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;
using AgentUp.Capabilities.Forgejo.Tests.Support;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Forge.E2E;

[TestFixture]
[Category("ForgejoE2E")]
[NonParallelizable]
[Timeout(180_000)]
[CancelAfter(180_000)]
public sealed class ForgejoForgeServiceE2ETests
{
    [OneTimeTearDown]
    public Task StopHost() => ForgejoLiveHost.StopSharedAsync();

    [Test]
    public async Task GetHostingAsync_returnsNoProposalForAConnectedRepositoryWithoutAPull()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("none"), CancellationToken.None);
        var (service, workspaceId) = await ArrangeAsync(host, repo, "main");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("noProposal"));
        Assert.That(hosting.ModuleId, Is.EqualTo(ForgejoForgeCapability.PackageId));
        Assert.That(hosting.Account!.Login, Is.EqualTo(ForgejoLiveHost.Owner));
    }

    [Test]
    public async Task GetHostingAsync_returnsReadyAfterOpenProposal()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("ready"), CancellationToken.None);
        var (service, workspaceId) = await ArrangeAsync(host, repo, "topic");
        var opened = await service.OpenProposalAsync(
            workspaceId,
            new ForgeOpenProposalRequest("Topic", "Body", "topic", "main"),
            CancellationToken.None);

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(opened!.Number, Is.EqualTo(hosting!.Proposal!.Number));
        Assert.That(hosting.Availability, Is.EqualTo("ready"));
        Assert.That(hosting.Proposal.HeadRef, Is.EqualTo("topic"));
    }

    [Test]
    public async Task ListIssuesAndAssigned_useTheLiveRepository()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("issues"), CancellationToken.None);
        await host.CreateIssueAsync(repo.Name, "Assigned", "do it", ForgejoLiveHost.Owner, CancellationToken.None);
        await host.CreateIssueAsync(repo.Name, "Unassigned", "later", null, CancellationToken.None);
        var (service, workspaceId) = await ArrangeAsync(host, repo, "main");

        var issues = await service.ListIssuesAsync(workspaceId, CancellationToken.None);
        var assigned = await service.ListAssignedAsync(workspaceId, CancellationToken.None);

        Assert.That(issues.Select(issue => issue.Title), Is.EquivalentTo(new[] { "Assigned", "Unassigned" }));
        Assert.That(assigned.Select(issue => issue.Title), Is.EqualTo(new[] { "Assigned" }));
    }

    [Test]
    public async Task CommentAndReview_roundTripOnTheOpenProposal()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("notes"), CancellationToken.None);
        var (service, workspaceId) = await ArrangeAsync(host, repo, "topic");
        var opened = await service.OpenProposalAsync(
            workspaceId,
            new ForgeOpenProposalRequest("Topic", "Body", "topic", "main"),
            CancellationToken.None);

        await service.CommentAsync(workspaceId, new ForgeCommentRequest(opened!.Number, "please fix"), CancellationToken.None);
        await service.SubmitReviewAsync(
            workspaceId,
            new ForgeReviewRequest(opened.Number, "lgtm", "COMMENT"),
            CancellationToken.None);
        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        var reviews = hosting!.Reviews ?? [];
        Assert.That(hosting.Threads.Select(thread => thread.Summary), Does.Contain("please fix"));
        Assert.That(reviews, Has.Count.EqualTo(1));
        Assert.That(reviews[0].Login, Is.EqualTo(ForgejoLiveHost.Owner));
    }

    [Test]
    public async Task UpdateRefAsync_returnsNoContentAndMovesTheBranch()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync(UniqueName("ref"), CancellationToken.None);
        var (service, workspaceId) = await ArrangeAsync(host, repo, "topic");

        var outcome = await service.UpdateRefAsync(
            workspaceId,
            new ForgeUpdateRefRequest("topic", repo.MainSha),
            CancellationToken.None);
        var sha = await host.ReadBranchShaAsync(repo.Name, "topic", CancellationToken.None);

        Assert.That(outcome.StatusCode, Is.EqualTo(204));
        Assert.That(sha, Is.EqualTo(repo.MainSha));
    }

    private static async Task<(ForgeService Service, string WorkspaceId)> ArrangeAsync(
        ForgejoLiveHost host,
        ForgejoLiveRepository repo,
        string branch)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Named(repo.Name).Build());
        var packages = new FakeEnabledCapabilityPackages().WithForges(host.Capability());
        var credentials = new MemoryForgeCredentialStore();
        credentials.Write(workspace.Id, ForgejoForgeCapability.PackageId, host.Token);
        var service = new ForgeService(
            workspaces,
            packages,
            credentials,
            new FixedForgeGitRemoteResolver(
                new ForgeGitRemote(repo.Owner, repo.Name, branch, repo.CloneUrl)),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("", "", "")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("exchanged"));
        return (service, workspace.Id);
    }

    private static string UniqueName(string prefix)
        => $"srv-{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
