using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Interfaces;
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
        Assert.That(hosting.Remedy, Does.Contain("Connect"));
    }

    [Test]
    public async Task GetHostingAsync_offersGithubAppSetupWithoutSharedOAuthSettings()
    {
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            githubApps: GithubApps());

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("needsCredential"));
        Assert.That(hosting.Detail, Does.Not.Contain("OAuth is not configured"));
        Assert.That(hosting.Remedy, Does.Contain("Connect"));
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

        var started = await service.StartLoginAsync(workspaceId);

        Assert.That(started.StatusCode, Is.EqualTo(200));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.StartWith("https://example.test/login/oauth/authorize"));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Contain("response_type=code"));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Contain("client_id=client"));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Contain("state="));
        Assert.That(((ForgeLoginStartDto)started.Body!).AuthorizationUrl, Does.Not.Contain("github.com"));
    }

    [Test]
    public async Task StartLogin_startsTheGithubManifestFlowWithoutAClientSecret()
    {
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            githubApps: GithubApps());

        var started = await service.StartLoginAsync(workspaceId, "http://localhost:5000");

        Assert.That(started.StatusCode, Is.EqualTo(200));
        Assert.That(
            ((ForgeLoginStartDto)started.Body!).AuthorizationUrl,
            Does.StartWith("http://localhost:5000/api/forge/github/register?state="));
    }

    [Test]
    public async Task GithubRegistrationPage_returnsNullWhenGithubAppsAreUnavailable()
    {
        var (service, _) = await ArrangeAsync();

        Assert.That(service.GithubRegistrationPage("state"), Is.Null);
    }

    [Test]
    public async Task GithubRegistrationPage_returnsTheManifestPage()
    {
        var store = new MemoryGithubAppStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            githubApps: new GithubAppConnectionService(store, new GithubAppApiProvider(new GithubOwnerHandler())));

        await service.StartLoginAsync(workspaceId, "http://localhost:5000");

        Assert.That(service.GithubRegistrationPage(store.Value!.State), Does.Contain("manifest"));
    }

    [Test]
    public async Task CompleteGithubManifestAsync_returnsNullWhenGithubAppsAreUnavailable()
    {
        var (service, _) = await ArrangeAsync();

        Assert.That(await service.CompleteGithubManifestAsync("code", "state"), Is.Null);
    }

    [Test]
    public async Task CompleteGithubInstallationAsync_returnsFalseWhenGithubAppsAreUnavailable()
    {
        var (service, _) = await ArrangeAsync();

        Assert.That(await service.CompleteGithubInstallationAsync("key", 1), Is.False);
    }

    [Test]
    public async Task StartLogin_returnsUnavailableWhenNoForgeIsEnabled()
    {
        var (service, workspaceId) = await ArrangeAsync(
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/api/forge/callback"));

        var started = await service.StartLoginAsync(workspaceId);

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

    [Test]
    public async Task StartLogin_returnsNotFoundForAnUnknownWorkspace()
    {
        var (service, _) = await ArrangeAsync(new StubForgeCapability());

        Assert.That((await service.StartLoginAsync("missing")).StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task StartLogin_returnsUnavailableWhenOAuthIsNotConfigured()
    {
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability());

        var started = await service.StartLoginAsync(workspaceId);

        Assert.That(started.StatusCode, Is.EqualTo(503));
        Assert.That(started.Body!.ToString(), Does.Contain("OAuth is not configured"));
    }

    [Test]
    public async Task StartLogin_returnsUnavailableWhenTheAuthorizeUrlIsBlank()
    {
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability
            {
                Host = new ForgeHostConfiguration("https://example.test/api/", "  ", "https://example.test/token")
            },
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback"));

        Assert.That((await service.StartLoginAsync(workspaceId)).StatusCode, Is.EqualTo(503));
    }

    [Test]
    public async Task StartLogin_appendsToAnAuthorizeUrlThatAlreadyHasAQuery()
    {
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability
            {
                Host = new ForgeHostConfiguration(
                    "https://example.test/api/",
                    "https://example.test/login/oauth/authorize?prompt=consent",
                    "https://example.test/token")
            },
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback"));

        var url = ((ForgeLoginStartDto)(await service.StartLoginAsync(workspaceId)).Body!).AuthorizationUrl;

        Assert.That(url, Does.Contain("authorize?prompt=consent&response_type=code&client_id=client"));
    }

    [Test]
    public async Task Logout_deletesTheStoredToken()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability(), credentials);
        credentials.Write(workspaceId, "github", "token");

        Assert.That(service.Logout(workspaceId), Is.True);
        Assert.That(credentials.Read(workspaceId, "github"), Is.Null);
    }

    [Test]
    public async Task Logout_succeedsWhenNoForgeIsEnabled()
    {
        var (service, workspaceId) = await ArrangeAsync();

        Assert.That(service.Logout(workspaceId), Is.True);
    }

    [Test]
    public async Task HasCredential_isFalseUntilATokenIsStored()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability(), credentials);

        Assert.That(service.HasCredential(workspaceId), Is.False);
        Assert.That(service.HasAnyCredential(), Is.False);
        credentials.Write(workspaceId, "github", "token");
        Assert.That(service.HasCredential(workspaceId), Is.True);
        Assert.That(service.HasAnyCredential(), Is.True);
    }

    [Test]
    public async Task HasCredential_isFalseWhenNoForgeIsEnabled()
    {
        var (service, workspaceId) = await ArrangeAsync();

        Assert.That(service.HasCredential(workspaceId), Is.False);
        Assert.That(service.HasAnyCredential(), Is.False);
    }

    [Test]
    public async Task HasCredential_treatsAWhitespaceTokenAsMissing()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability(), credentials);
        credentials.Write(workspaceId, "github", "   ");

        Assert.That(service.HasCredential(workspaceId), Is.False);
        Assert.That(service.HasAnyCredential(), Is.False);
        Assert.That(service.HasCredential("missing"), Is.False);
    }

    [Test]
    public async Task CompleteLoginAsync_returnsFalseForAnUnknownState()
    {
        var (service, _) = await ArrangeAsync(new StubForgeCapability());

        Assert.That(await service.CompleteLoginAsync("code", "missing", CancellationToken.None), Is.False);
    }

    [Test]
    public async Task GetHostingAsync_usesDefaultUnreachableCopyWhenTheHostMessageIsBlank()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability { ReadFailure = new HttpRequestException(" ") },
            credentials);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Detail, Is.EqualTo(ForgeHostingCopy.UnreachableDetail));
        Assert.That(hosting.Remedy, Does.Contain("retry"));
    }

    [Test]
    public async Task GetHostingAsync_returnsReadyWithoutMergeability()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability
            {
                Snapshot = new ForgeHostSnapshot(
                    new ForgeProposal("1", "42", "Ready", "https://example.test/42", "topic", "main", "open"),
                    [],
                    [],
                    null)
            },
            credentials);
        credentials.Write(workspaceId, "github", "token");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("ready"));
        Assert.That(hosting.Mergeability, Is.Null);
    }

    [Test]
    public async Task ListAssignedAsync_returnsEmptyWhenTheRequestCannotBeBuilt()
    {
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability());

        Assert.That(await service.ListAssignedAsync(workspaceId, CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task CommentAndReview_runWhenTheRequestCanBeBuilt()
    {
        var credentials = new MemoryForgeCredentialStore();
        var forge = new StubForgeCapability();
        var (service, workspaceId) = await ArrangeAsync(forge, credentials);
        credentials.Write(workspaceId, "github", "token");

        await service.CommentAsync(workspaceId, new ForgeCommentRequest("42", "noted"), CancellationToken.None);
        await service.SubmitReviewAsync(
            workspaceId,
            new ForgeReviewRequest("42", "lgtm", "COMMENT"),
            CancellationToken.None);

        Assert.That(forge.CommentCalls, Is.EqualTo(1));
        Assert.That(forge.ReviewCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task OpenProposalAsync_returnsTheOpenedProposal()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability(), credentials);
        credentials.Write(workspaceId, "github", "token");

        var opened = await service.OpenProposalAsync(
            workspaceId,
            new ForgeOpenProposalRequest("Title", "Body", "topic", "main"),
            CancellationToken.None);

        Assert.That(opened!.Title, Is.EqualTo("Title"));
        Assert.That(opened.HeadRef, Is.EqualTo("topic"));
    }

    [Test]
    public async Task UpdateRefAsync_returnsNoContentForAnUnprotectedRef()
    {
        var credentials = new MemoryForgeCredentialStore();
        var forge = new StubForgeCapability();
        var (service, workspaceId) = await ArrangeAsync(forge, credentials);
        credentials.Write(workspaceId, "github", "token");

        var outcome = await service.UpdateRefAsync(
            workspaceId,
            new ForgeUpdateRefRequest("topic", "abc"),
            CancellationToken.None);

        Assert.That(outcome.StatusCode, Is.EqualTo(204));
        Assert.That(forge.UpdateRefCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task Logout_returnsFalseForAnUnknownWorkspace()
    {
        var (service, _) = await ArrangeAsync(new StubForgeCapability());

        Assert.That(service.Logout("missing"), Is.False);
    }

    [Test]
    public async Task CompleteLoginAsync_returnsFalseWhenTheWorkspaceNoLongerExists()
    {
        var states = new ForgeOAuthStateStore();
        var (service, _) = await ArrangeAsync(new StubForgeCapability(), states: states);
        var state = states.Issue("missing");

        Assert.That(await service.CompleteLoginAsync("code", state, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task GetHostingAsync_treatsAWhitespaceTokenAsMissingCredential()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability(),
            credentials,
            oauth: new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback"));
        credentials.Write(workspaceId, "github", "   ");

        var hosting = await service.GetHostingAsync(workspaceId, CancellationToken.None);

        Assert.That(hosting!.Availability, Is.EqualTo("needsCredential"));
        Assert.That(hosting.Remedy, Does.Contain("Connect"));
    }

    [Test]
    public async Task GetHostingAsync_doesNotMapCallerCancellationToUnreachable()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(
            new StubForgeCapability { ReadFailure = new TaskCanceledException("canceled") },
            credentials);
        credentials.Write(workspaceId, "github", "token");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(
            () => service.GetHostingAsync(workspaceId, canceled.Token));
    }

    [Test]
    public async Task ListAssignedAsync_returnsEmptyWhenTheStoredTokenIsBlank()
    {
        var credentials = new MemoryForgeCredentialStore();
        var (service, workspaceId) = await ArrangeAsync(new StubForgeCapability(), credentials);
        credentials.Write(workspaceId, "github", " ");

        Assert.That(await service.ListAssignedAsync(workspaceId, CancellationToken.None), Is.Empty);
    }

    private static async Task<(ForgeService Service, string WorkspaceId)> ArrangeAsync(
        StubForgeCapability? forge = null,
        MemoryForgeCredentialStore? credentials = null,
        ForgeGitRemote? remote = null,
        bool noRemote = false,
        ForgeOAuthConfiguration? oauth = null,
        ForgeOAuthStateStore? states = null,
        FixedForgeOAuthExchange? exchange = null,
        GithubAppConnectionService? githubApps = null)
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
            exchange ?? new FixedForgeOAuthExchange("exchanged"),
            githubApps);
        return (service, workspace.Id);
    }

    private static GithubAppConnectionService GithubApps()
        => new(new MemoryGithubAppStore(), new GithubAppApiProvider(new GithubOwnerHandler()));

    private sealed class MemoryGithubAppStore : IGithubAppConnectionStore
    {
        public GithubAppConnection? Value { get; private set; }
        public GithubAppConnection? Read(string workspaceId) => Value?.WorkspaceId == workspaceId ? Value : null;
        public GithubAppConnection? FindByState(string state) => Value?.State == state ? Value : null;
        public GithubAppConnection? FindBySetupKey(string setupKey) => Value?.SetupKey == setupKey ? Value : null;
        public void Write(GithubAppConnection connection) => Value = connection;
        public void Delete(string workspaceId) => Value = null;
    }

    private sealed class GithubOwnerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"type\":\"User\"}")
            });
    }
}
