using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Forge.Unit;

[TestFixture]
public sealed class ForgeServiceTests
{
    [Test]
    public async Task GetHostingAsync_returnsNoneWhenNoForgeIsEnabled()
    {
        var (service, workspaceId) = await ArrangeAsync();

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("none"));
        Assert.That(hosting.ModuleId, Is.Null);
    }

    [Test]
    public async Task GetHostingAsync_returnsNeedsCredentialWhenTheStoreIsEmpty()
    {
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability());

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("needsCredential"));
        Assert.That(hosting.ModuleId, Is.EqualTo("github"));
    }

    [Test]
    public async Task GetHostingAsync_returnsUnreachableWhenTheRemoteCannotBeRead()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            credentials,
            noRemote: true);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("unreachable"));
        Assert.That(hosting.Detail, Does.Contain("origin"));
    }

    [Test]
    public async Task GetHostingAsync_returnsNoProposalWhenTheHostHasNone()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability(), credentials);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("noProposal"));
    }

    [Test]
    public async Task GetHostingAsync_returnsReadyWithThreadsAndMergeability()
    {
        var credentials = new MemoryForgeCredentialStore();
        var forge = new StubForgeCapability
        {
            Snapshot = new ForgeHostSnapshot(
                new ForgeProposal("1", "42", "Ready", "https://example.test/42", "topic", "main", "open"),
                [new ForgeCheck("ci", "completed", "success", "https://example.test/check")],
                [new ForgeReviewThread("PRRT_1", "https://example.test/thread", "nits", false)],
                new ForgeMergeability(true, "clean"))
        };
        var (service, workspaceId) = await ArrangeAsync(forge, credentials);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(hosting!.Availability, Is.EqualTo("ready"));
            Assert.That(hosting.Proposal!.Number, Is.EqualTo("42"));
            Assert.That(hosting.Checks.Single().Conclusion, Is.EqualTo("success"));
            Assert.That(hosting.Threads.Single().Id, Is.EqualTo("PRRT_1"));
            Assert.That(hosting.Mergeability!.Mergeable, Is.True);
        });
    }

    [Test]
    public async Task GetHostingAsync_mapsHostFailuresToUnreachable()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability { ReadFailure = new HttpRequestException("rate limited") },
            credentials);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("unreachable"));
        Assert.That(hosting.Detail, Does.Contain("rate limited"));
    }

    [Test]
    public async Task GetHostingAsync_returnsNullForAnUnknownWorkspace()
    {
        var (service, _) = await ArrangeAsync();

        Assert.That(await service.GetHostingAsync("missing", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task StartLogin_issuesAStateBoundAuthorizationUrl()
    {
        var states = new ForgeOAuthStateStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/api/forge/callback"),
            states: states);

        var started = service.StartLogin(workspaceId);

        Assert.That(started.StatusCode, Is.EqualTo(200));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Contain("client_id=client"));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Contain("state="));
    }

    [Test]
    public async Task CompleteLoginAsync_storesTheExchangedToken()
    {
        var credentials = new MemoryForgeCredentialStore();
        var states = new ForgeOAuthStateStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            credentials,
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback"),
            states: states);
        var state = states.Issue(workspaceId);

        var completed = await service.CompleteLoginAsync("code", state, CancellationToken.None);

        Assert.That(completed, Is.True);
        Assert.That(credentials.Read(workspaceId, "github"), Is.EqualTo("exchanged"));
    }

    private static async Task<(ForgeService Service, string WorkspaceId)> ArrangeAsync(
        StubForgeCapability? forge = null,
        MemoryForgeCredentialStore? credentials = null,
        ForgeGitRemote? remote = null,
        bool noRemote = false,
        ForgeOAuthConfiguration? oauth = null,
        ForgeOAuthStateStore? states = null)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var packages = new FakeEnabledCapabilityPackages();
        if (forge is not null)
            packages.WithForges(forge);
        var resolved = noRemote
            ? null
            : remote ?? (forge is null ? null : new ForgeGitRemote("acme", "widgets", "main", "https://github.com/acme/widgets.git"));
        var service = new ForgeService(
            workspaces,
            packages,
            credentials ?? new MemoryForgeCredentialStore(),
            new FixedForgeGitRemoteResolver(resolved),
            new FixedForgeOAuthSettings(oauth ?? new ForgeOAuthConfiguration("", "", "")),
            states ?? new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("exchanged"));
        return (service, workspace.Id);
    }
}
