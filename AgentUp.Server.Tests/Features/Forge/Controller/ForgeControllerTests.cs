using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Forge.Controller;

[TestFixture]
public sealed class ForgeControllerTests
{
    [Test]
    public async Task GetHostingAsync_returnsTheServiceDocument()
    {
        var (controller, workspaceId, _) = await ArrangeAsync();

        var hosting = await controller.GetHostingAsync(workspaceId);

        Assert.That(hosting!.Availability, Is.EqualTo("ready"));
        Assert.That(hosting.Threads.Single().Id, Is.EqualTo("PRRT_1"));
    }

    [Test]
    public async Task OpenProposalAsync_returnsTheOpenedProposal()
    {
        var (controller, workspaceId, _) = await ArrangeAsync();

        var proposal = await controller.OpenProposalAsync(
            workspaceId,
            new ForgeOpenProposalRequest("Title", "Body", "topic", "main"));

        Assert.That(proposal!.Number, Is.EqualTo("42"));
        Assert.That(proposal.Title, Is.EqualTo("Title"));
    }

    [Test]
    public async Task UpdateRefAsync_reachesTheEnabledModule()
    {
        var (controller, workspaceId, forge) = await ArrangeAsync();

        await controller.UpdateRefAsync(workspaceId, new ForgeUpdateRefRequest("topic", "abc"));

        Assert.That(forge.UpdateRefCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task CommentAndReview_reachTheEnabledModule()
    {
        var (controller, workspaceId, forge) = await ArrangeAsync();

        await controller.CommentAsync(workspaceId, new ForgeCommentRequest("42", "looks good"));
        await controller.SubmitReviewAsync(workspaceId, new ForgeReviewRequest("42", "approve", "APPROVE"));

        Assert.That(forge.CommentCalls, Is.EqualTo(1));
        Assert.That(forge.ReviewCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Logout_clearsTheStoredCredential()
    {
        var (controller, workspaceId, _) = await ArrangeAsync();

        Assert.That(controller.Logout(workspaceId), Is.True);
        var hosting = await controller.GetHostingAsync(workspaceId);
        Assert.That(hosting!.Availability, Is.EqualTo("needsCredential"));
    }

    [Test]
    public async Task GithubRegistrationPage_returnsNullWhenGithubAppsAreUnavailable()
    {
        var (controller, _, _) = await ArrangeAsync();

        Assert.That(controller.GithubRegistrationPage("state"), Is.Null);
    }

    [Test]
    public async Task HasCredential_forwardsTheStoredForgeToken()
    {
        var (controller, workspaceId, _) = await ArrangeAsync();

        Assert.That(controller.HasCredential(workspaceId), Is.True);
        Assert.That(controller.HasAnyCredential(), Is.True);
        Assert.That(controller.Logout(workspaceId), Is.True);
        Assert.That(controller.HasCredential(workspaceId), Is.False);
        Assert.That(controller.HasAnyCredential(), Is.False);
    }

    private static async Task<(ForgeController Controller, string WorkspaceId, StubForgeCapability Forge)> ArrangeAsync()
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var credentials = new MemoryForgeCredentialStore();
        credentials.Write(workspace.Id, "github", "token");
        var forge = new StubForgeCapability
        {
            Snapshot = new ForgeHostSnapshot(
                new ForgeProposal("1", "42", "Ready", "https://example.test/42", "topic", "main", "open"),
                [],
                [new ForgeReviewThread("PRRT_1", "https://example.test/thread", "nits", false)],
                new ForgeMergeability(true, "clean"))
        };
        var service = new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(forge),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("token"));
        return (new ForgeController(service), workspace.Id, forge);
    }
}
