using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.TaskSource.Unit;

[TestFixture]
public sealed class TaskSourceServiceTests
{
    [Test]
    public void DescribeListsEachTeamMemberAndNeverAToken()
    {
        var described = Service().Describe();

        Assert.Multiple(() =>
        {
            Assert.That(described.Identities.Select(identity => identity.WorkspaceId), Is.EqualTo(new[] { "bob", "frank" }));
            Assert.That(described.Identities.Single(identity => identity.WorkspaceId == "bob").Initials, Is.EqualTo("BO"));
            Assert.That(described.Identities.Single(identity => identity.WorkspaceId == "bob").Connected, Is.False);
            Assert.That(described.Identities.Single(identity => identity.WorkspaceId == "frank").Detail, Does.Contain("Frank"));
        });
    }

    [Test]
    public async Task ATokenThatCanReadPullRequestsIsStoredForThatMemberAndNotEchoed()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var forge = new StubForgeCapability();
        var service = Service(forge, credentials);

        var saved = await service.SaveAsync(Save("bob", "github", " secret-token ", " https://github.com/ "), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(200));
            Assert.That(credentials.Read("bob")?.Token, Is.EqualTo("secret-token"));
            Assert.That(credentials.Read("bob")?.EndpointUrl, Is.EqualTo("https://github.com"));
            Assert.That(credentials.Read("bob")?.ModuleId, Is.EqualTo("github"));
            Assert.That(forge.LastEndpointUrl, Is.EqualTo("https://github.com"));
            var bob = service.Describe().Identities.Single(identity => identity.WorkspaceId == "bob");
            Assert.That(bob.Connected, Is.True);
            Assert.That(bob.ModuleId, Is.EqualTo("github"));
            Assert.That(bob.EndpointUrl, Is.EqualTo("https://github.com"));
            Assert.That(bob.Detail, Does.Not.Contain("secret-token"));
            Assert.That(bob.Detail, Does.Contain("Bob"));
        });
    }

    [Test]
    public async Task AMissingHostUrlIsRefusedBecauseTheTokenHasNowhereToConnect()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var saved = await Service(credentials: credentials).SaveAsync(Save("bob", "github", "token", " "), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(409));
            Assert.That(saved.Body!.ToString(), Does.Contain("host URL"));
            Assert.That(credentials.Read("bob"), Is.Null);
        });
    }

    [Test]
    public async Task ANonHttpHostUrlIsRefused()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var saved = await Service(credentials: credentials).SaveAsync(Save("bob", "forgejo", "token", "git.example.com"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(409));
            Assert.That(saved.Body!.ToString(), Does.Contain("http"));
            Assert.That(credentials.Read("bob"), Is.Null);
        });
    }

    [Test]
    public async Task ATokenThatCannotReadPullRequestsIsRefusedAndNotStored()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var forge = new StubForgeCapability { TokenAccess = new ForgeTokenAccess(true, false, "The token cannot read pull requests.") };
        var saved = await Service(forge, credentials).SaveAsync(Save("bob", "github", "narrow"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(409));
            Assert.That(saved.Body!.ToString(), Does.Contain("pull requests"));
            Assert.That(credentials.Read("bob"), Is.Null);
        });
    }

    [Test]
    public async Task ForgettingOneMemberKeepsTheOther()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges(
            new StubForgeCapability(),
            new StubForgeCapability { Identity = new("forgejo", "1.0.0", "Forgejo", "agent-up") });
        var service = new TaskSourceService(credentials, packages, new FixedTeam(new Dictionary<string, string>(), []), People());
        await service.SaveAsync(Save("bob", "github", "github-token"), CancellationToken.None);
        await service.SaveAsync(Save("frank", "forgejo", "forgejo-token", "https://git.example.test"), CancellationToken.None);

        service.Clear("bob");

        Assert.Multiple(() =>
        {
            Assert.That(credentials.Read("bob"), Is.Null);
            Assert.That(credentials.Read("frank")?.Token, Is.EqualTo("forgejo-token"));
            Assert.That(credentials.Read("frank")?.EndpointUrl, Is.EqualTo("https://git.example.test"));
            Assert.That(credentials.Read("frank")?.ModuleId, Is.EqualTo("forgejo"));
        });
    }

    [Test]
    public async Task ALegacyTokenWithoutAHostIsNotConnectedAndListsNothing()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        credentials.Write("bob", new TaskSourceCredential("legacy-token", "", "github"));
        var service = Service(credentials: credentials);

        var bob = service.Describe().Identities.Single(identity => identity.WorkspaceId == "bob");
        var repositories = await service.ListRepositoriesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(bob.Connected, Is.False);
            Assert.That(bob.Detail, Does.Contain("host URL"));
            Assert.That(repositories, Is.Empty);
        });
    }

    [Test]
    public async Task AnEmptyTokenIsRefusedEvenWhenTheHostUrlIsPresent()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var saved = await Service(credentials: credentials).SaveAsync(Save("bob", "github", "  ", "https://github.com"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(409));
            Assert.That(saved.Body!.ToString(), Does.Contain("personal access token"));
            Assert.That(credentials.Read("bob"), Is.Null);
        });
    }

    [Test]
    public async Task RepositoriesAreTheUnionOfWhatEveryMemberCanSee()
    {
        var forge = new StubForgeCapability { Repositories = [new ForgeRepository("acme", "widgets"), new ForgeRepository("acme", "shared")] };
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = Service(forge, credentials);
        await service.SaveAsync(Save("bob", "github", "token", "https://ghe.example.test"), CancellationToken.None);
        await service.SaveAsync(Save("frank", "github", "other", "https://ghe.example.test"), CancellationToken.None);

        var repositories = await service.ListRepositoriesAsync(CancellationToken.None);
        var widgets = repositories.Single(repository => repository.Name == "widgets");

        Assert.Multiple(() =>
        {
            Assert.That(repositories.Select(repository => $"{repository.ModuleId}:{repository.Owner}/{repository.Name}"), Is.EqualTo(new[] { "github:acme/widgets", "github:acme/shared" }));
            Assert.That(widgets.Members!.Select(member => member.WorkspaceId), Is.EqualTo(new[] { "bob", "frank" }));
            Assert.That(widgets.Members.Select(member => member.Initials), Is.EqualTo(new[] { "BO", "FR" }));
            Assert.That(forge.LastEndpointUrl, Is.EqualTo("https://ghe.example.test"));
        });
    }

    [Test]
    public async Task ALegacyHostTokenStillListsRepositoriesWithoutNamingAMember()
    {
        var forge = new StubForgeCapability { Repositories = [new ForgeRepository("acme", "widgets")] };
        var credentials = new MemoryTaskSourceCredentialStore();
        credentials.Write("github", new TaskSourceCredential("legacy", "https://github.com", "github"));
        var service = Service(forge, credentials);

        var repositories = await service.ListRepositoriesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repositories.Single().Name, Is.EqualTo("widgets"));
            Assert.That(repositories.Single().Members, Is.Empty);
        });
    }

    [Test]
    public async Task AnUnknownHostIsRefused()
    {
        var saved = await Service().SaveAsync(Save("bob", "gitlab", "token"), CancellationToken.None);

        Assert.That(saved.StatusCode, Is.EqualTo(409));
        Assert.That(saved.Body!.ToString(), Does.Contain("GitHub or Forgejo"));
    }

    [Test]
    public async Task AnUnknownMemberIsRefused()
    {
        var saved = await Service().SaveAsync(Save("mia", "github", "token"), CancellationToken.None);

        Assert.That(saved.StatusCode, Is.EqualTo(409));
        Assert.That(saved.Body!.ToString(), Does.Contain("team member"));
    }

    [Test]
    public async Task ATokenIsRefusedUntilTheHostCapabilityIsEnabled()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = new TaskSourceService(credentials, new FakeEnabledCapabilityPackages(), new EmptyTaskSourceTeam(), People());

        var saved = await service.SaveAsync(Save("bob", "github", "token"), CancellationToken.None);

        Assert.That(saved.StatusCode, Is.EqualTo(409));
        Assert.That(saved.Body!.ToString(), Does.Contain("Enable the GitHub"));
        Assert.That(credentials.Read("bob"), Is.Null);
    }

    [Test]
    public async Task AHostThatCannotBeReachedIsReportedAsUnavailable()
    {
        var forge = new StubForgeCapability { TokenAccessFailure = new HttpRequestException("offline") };
        var saved = await Service(forge).SaveAsync(Save("bob", "github", "token"), CancellationToken.None);

        Assert.That(saved.StatusCode, Is.EqualTo(503));
        Assert.That(saved.Body!.ToString(), Does.Contain("Could not reach GitHub"));
    }

    [Test]
    public async Task ForgettingEveryMemberClearsEveryToken()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = Service(credentials: credentials);
        await service.SaveAsync(Save("bob", "github", "github-token"), CancellationToken.None);

        service.Clear(null);

        Assert.That(credentials.Read("bob"), Is.Null);
        Assert.That(service.Describe().Identities.Select(identity => identity.Connected), Is.All.False);
    }

    [Test]
    public void AnEmptySavedTokenWithTheCapabilityDisabledAsksToEnableThenPaste()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        credentials.Write("bob", new TaskSourceCredential("", "https://github.com", "github"));
        var service = new TaskSourceService(credentials, new FakeEnabledCapabilityPackages(), new EmptyTaskSourceTeam(), People());

        var bob = service.Describe().Identities.Single(identity => identity.WorkspaceId == "bob");

        Assert.That(bob.Detail, Does.Contain("Enable the GitHub capability, then paste"));
    }

    [Test]
    public async Task ALegacyHostKeyedTokenStillListsRepositories()
    {
        var forge = new StubForgeCapability { Repositories = [new ForgeRepository("acme", "widgets")] };
        var credentials = new MemoryTaskSourceCredentialStore();
        credentials.Write("github", new TaskSourceCredential("token", "https://github.com", ""));
        var service = Service(forge, credentials);

        var repositories = await service.ListRepositoriesAsync(CancellationToken.None);

        Assert.That(repositories.Single().Name, Is.EqualTo("widgets"));
    }

    [Test]
    public async Task ASavedTokenWithTheCapabilityDisabledSaysToEnableIt()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        credentials.Write("bob", new TaskSourceCredential("token", "https://github.com", "github"));
        var service = new TaskSourceService(credentials, new FakeEnabledCapabilityPackages(), new EmptyTaskSourceTeam(), People());

        var bob = service.Describe().Identities.Single(identity => identity.WorkspaceId == "bob");

        Assert.That(bob.Connected, Is.False);
        Assert.That(bob.Detail, Does.Contain("Enable the GitHub capability to use Bob's saved token"));
    }

    [Test]
    public async Task AHostThatCannotBeReachedListsNoRepositories()
    {
        var forge = new StubForgeCapability
        {
            Repositories = [new ForgeRepository("acme", "widgets")],
            ListRepositoriesFailure = new HttpRequestException("offline"),
        };
        var service = Service(forge);
        await service.SaveAsync(Save("bob", "github", "token"), CancellationToken.None);

        Assert.That(await service.ListRepositoriesAsync(CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task IssuesAndPullsAreEmptyWhenTheHostOrRepositoryIsMissing()
    {
        var service = Service();
        await service.SaveAsync(Save("bob", "github", "token"), CancellationToken.None);
        var unknown = await service.ListIssuesAsync("gitlab", "acme", "widgets", CancellationToken.None);
        var noOwner = await service.ListIssuesAsync("github", "", "widgets", CancellationToken.None);
        var noRepo = await service.ListPullsAsync("github", "acme", "", CancellationToken.None);

        Assert.That(unknown, Is.Empty);
        Assert.That(noOwner, Is.Empty);
        Assert.That(noRepo, Is.Empty);
    }

    [Test]
    public async Task AHostThatCannotBeReachedListsNoIssuesOrPulls()
    {
        var forge = new StubForgeCapability
        {
            ListIssuesFailure = new HttpRequestException("offline"),
            ListPullRequestsFailure = new TaskCanceledException(),
        };
        var service = Service(forge);
        await service.SaveAsync(Save("bob", "github", "token"), CancellationToken.None);

        Assert.That(await service.ListIssuesAsync("github", "acme", "widgets", CancellationToken.None), Is.Empty);
        Assert.That(await service.ListPullsAsync("github", "acme", "widgets", CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task IssuesAndPullsAreReadFromTheNamedRepositoryAtTheSavedHost()
    {
        var forge = new StubForgeCapability
        {
            Issues = [new ForgeIssue("I-7", 7, "Cart badge", "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "https://example.test/7", "bob")],
            PullRequests =
            [
                new ForgePullRequest("42", 42, "Product cards", "https://example.test/42", ["hector"], []),
            ],
        };
        var service = Service(forge, team: new FixedTeam(new Dictionary<string, string> { ["hector"] = "devReviewer" }, ["devReviewer"]));
        await service.SaveAsync(Save("bob", "github", "token", "https://github.com"), CancellationToken.None);

        var issues = await service.ListIssuesAsync("github", "acme", "widgets", CancellationToken.None);
        var pulls = await service.ListPullsAsync("github", "acme", "widgets", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(issues.Single().Number, Is.EqualTo(7));
            Assert.That(issues.Single().AssigneeLogin, Is.EqualTo("bob"));
            Assert.That(pulls.Single().Reviews.Single().Standing, Is.EqualTo("reviewing"));
            Assert.That(pulls.Single().Reviews.Single().Kind, Is.EqualTo("devReviewer"));
            Assert.That(forge.LastEndpointUrl, Is.EqualTo("https://github.com"));
        });
    }

    private static SaveTaskSourceTokenRequest Save(string workspaceId, string moduleId, string token, string endpoint = "https://github.com")
        => new(workspaceId, moduleId, endpoint, token);

    private static FixedTaskSourceIdentities People()
        => new(new TaskSourcePerson("bob", "Bob"), new TaskSourcePerson("frank", "Frank"));

    private static TaskSourceService Service(
        StubForgeCapability? forge = null,
        MemoryTaskSourceCredentialStore? credentials = null,
        FixedTeam? team = null)
    {
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges(forge ?? new StubForgeCapability());
        return new TaskSourceService(
            credentials ?? new MemoryTaskSourceCredentialStore(),
            packages,
            team ?? new FixedTeam(new Dictionary<string, string>(), []),
            People());
    }

    private sealed class FixedTeam(IReadOnlyDictionary<string, string> kindByLogin, IReadOnlyCollection<string> kinds)
        : AgentUp.Server.Features.TaskSource.Interfaces.ITaskSourceTeam
    {
        public Task<AgentUp.Server.Features.TaskSource.Interfaces.TaskSourceTeam> ForAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken)
            => Task.FromResult(new AgentUp.Server.Features.TaskSource.Interfaces.TaskSourceTeam(kindByLogin, kinds));
    }
}
