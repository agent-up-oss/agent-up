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
    public async Task GetHostingAsync_returnsNotInstalledWhenNoForgeIsEnabled()
    {
        var (service, workspaceId) = await ArrangeAsync();

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("notInstalled"));
        Assert.That(hosting.ModuleId, Is.Null);
        Assert.That(hosting.Detail, Does.Contain("forge capability"));
        Assert.That(hosting.Remedy, Does.Contain("capabilities bootstrap"));
    }

    [Test]
    public async Task GetHostingAsync_returnsNeedsCredentialWhenTheStoreIsEmpty()
    {
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability());

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("needsCredential"));
        Assert.That(hosting.ModuleId, Is.EqualTo("github"));
        Assert.That(hosting.Detail, Does.Contain("OAuth"));
        Assert.That(hosting.Remedy, Does.Contain("AGENTUP_FORGE_CLIENT_ID"));
    }

    [Test]
    public async Task GetHostingAsync_asksForSignInWhenOAuthIsConfigured()
    {
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback"));

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("needsCredential"));
        Assert.That(hosting.Detail, Does.Contain("credential"));
        Assert.That(hosting.Remedy, Does.Contain("Sign in"));
    }

    [Test]
    public async Task GetHostingAsync_returnsNoRemoteWhenTheRemoteCannotBeRead()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            credentials,
            noRemote: true);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("noRemote"));
        Assert.That(hosting.Detail, Does.Contain("origin"));
        Assert.That(hosting.Remedy, Does.Contain("git remote"));
    }

    [Test]
    public async Task GetHostingAsync_returnsNoProposalWhenTheHostHasNone()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability(), credentials);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("noProposal"));
        Assert.That(hosting.Detail, Does.Contain("No open proposal"));
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
            Assert.That(hosting.Proposal!.Id, Is.EqualTo("1"));
            Assert.That(hosting.Proposal.Number, Is.EqualTo("42"));
            Assert.That(hosting.Proposal.Title, Is.EqualTo("Ready"));
            Assert.That(hosting.Proposal.Url, Does.Contain("/42"));
            Assert.That(hosting.Proposal.HeadRef, Is.EqualTo("topic"));
            Assert.That(hosting.Proposal.BaseRef, Is.EqualTo("main"));
            Assert.That(hosting.Proposal.State, Is.EqualTo("open"));
            Assert.That(hosting.Checks.Single().Conclusion, Is.EqualTo("success"));
            Assert.That(hosting.Threads.Single().Id, Is.EqualTo("PRRT_1"));
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
        Assert.That(hosting.Remedy, Does.Contain("retry"));
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
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.StartWith("https://example.test/login/oauth/authorize"));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Contain("client_id=client"));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Contain("state="));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Not.Contain("github.com"));
    }

    [Test]
    public async Task StartLogin_returnsUnavailableWhenNoForgeIsEnabled()
    {
        var (service, workspaceId) = await ArrangeAsync(
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/api/forge/callback"));

        var started = service.StartLogin(workspaceId);

        Assert.That(started.StatusCode, Is.EqualTo(503));
    }

    [Test]
    public async Task CompleteLoginAsync_storesTheExchangedToken()
    {
        var credentials = new MemoryForgeCredentialStore();
        var states = new ForgeOAuthStateStore();
        var exchange = new FixedForgeOAuthExchange("exchanged");
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            credentials,
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback"),
            states: states,
            exchange: exchange);
        var state = states.Issue(workspaceId);

        var completed = await service.CompleteLoginAsync("code", state, CancellationToken.None);

        Assert.That(completed, Is.True);
        Assert.That(credentials.Read(workspaceId, "github"), Is.EqualTo("exchanged"));
        Assert.That(exchange.LastTokenUrl, Is.EqualTo("https://example.test/login/oauth/access_token"));
    }

    [Test]
    public async Task GetHostingAsync_mapsATimeoutToUnreachable()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability { ReadFailure = new TaskCanceledException("timed out") },
            credentials);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("unreachable"));
        Assert.That(hosting.Detail, Does.Contain("timed out"));
    }

    [Test]
    public async Task CompleteLoginAsync_returnsFalseWhenNoForgeIsEnabled()
    {
        var states = new ForgeOAuthStateStore();
        var (service, workspaceId) = await ArrangeAsync(states: states);
        var state = states.Issue(workspaceId);

        Assert.That(await service.CompleteLoginAsync("code", state, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task ListAssignedAsync_returnsEmptyWhenTheHostFails()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability { ListAssignedFailure = new HttpRequestException("offline") },
            credentials);
        credentials.Write(workspaceId, "github", "token");

        Assert.That(await service.ListAssignedAsync(workspaceId, CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task UpdateRefAsync_returnsNotFoundWhenTheRequestCannotBeBuilt()
    {
        var (service, workspaceId) = await ArrangeAsync();

        var outcome = await service.UpdateRefAsync(
            workspaceId,
            new ForgeUpdateRefRequest("topic", "abc"),
            CancellationToken.None);

        Assert.That(outcome.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task UpdateRefAsync_returnsBadRequestForAProtectedRef()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability { UpdateRefFailure = new ForgeProtectedRefException("main") },
            credentials);
        credentials.Write(workspaceId, "github", "token");

        var outcome = await service.UpdateRefAsync(
            workspaceId,
            new ForgeUpdateRefRequest("main", "abc"),
            CancellationToken.None);

        Assert.That(outcome.StatusCode, Is.EqualTo(400));
        Assert.That(outcome.Body, Is.Not.Null);
    }

    [Test]
    public async Task OpenProposalAsync_returnsNullWhenTheRequestCannotBeBuilt()
    {
        var (service, workspaceId) = await ArrangeAsync();

        Assert.That(
            await service.OpenProposalAsync(
                workspaceId,
                new ForgeOpenProposalRequest("Title", "Body", "topic", "main"),
                CancellationToken.None),
            Is.Null);
    }

    private static async Task<(ForgeService Service, string WorkspaceId)> ArrangeAsync(
        StubForgeCapability? forge = null,
        MemoryForgeCredentialStore? credentials = null,
        ForgeGitRemote? remote = null,
        bool noRemote = false,
        ForgeOAuthConfiguration? oauth = null,
        ForgeOAuthStateStore? states = null,
        FixedForgeOAuthExchange? exchange = null)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var packages = new FakeEnabledCapabilityPackages();
        if (forge is not null)
            packages.WithForges(forge);
        var resolved = noRemote
            ? null
            : remote ?? (forge is null ? null : new ForgeGitRemote("acme", "widgets", "main", "https://example.test/acme/widgets.git"));
        var service = new ForgeService(
            workspaces,
            packages,
            credentials ?? new MemoryForgeCredentialStore(),
            new FixedForgeGitRemoteResolver(resolved),
            new FixedForgeOAuthSettings(oauth ?? new ForgeOAuthConfiguration("", "", "")),
            states ?? new ForgeOAuthStateStore(),
            exchange ?? new FixedForgeOAuthExchange("exchanged"));
        return (service, workspace.Id);
    }
}
