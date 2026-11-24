using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.TaskSource.Unit;

[TestFixture]
public sealed class TaskSourceServiceTests
{
    [Test]
    public void DescribeListsBothHostsAndNeverAToken()
    {
        var described = Service().Describe();

        Assert.Multiple(() =>
        {
            Assert.That(described.Modules.Select(module => module.ModuleId), Is.EqualTo(new[] { "github", "forgejo" }));
            Assert.That(described.Modules.Single(module => module.ModuleId == "github").Connected, Is.False);
            Assert.That(described.Modules.Single(module => module.ModuleId == "forgejo").Detail, Does.Contain("Enable the Forgejo"));
        });
    }

    [Test]
    public async Task ATokenThatCanReadPullRequestsIsStoredWithItsHostAndNotEchoed()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var forge = new StubForgeCapability();
        var service = Service(forge, credentials);

        var saved = await service.SaveAsync(Save("github", " secret-token ", " https://github.com/ "), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(200));
            Assert.That(credentials.Read("github")?.Token, Is.EqualTo("secret-token"));
            Assert.That(credentials.Read("github")?.EndpointUrl, Is.EqualTo("https://github.com"));
            Assert.That(forge.LastEndpointUrl, Is.EqualTo("https://github.com"));
            Assert.That(service.Describe().Modules.Single(module => module.ModuleId == "github").Connected, Is.True);
            Assert.That(service.Describe().Modules.Single(module => module.ModuleId == "github").EndpointUrl, Is.EqualTo("https://github.com"));
            Assert.That(service.Describe().Modules.Single(module => module.ModuleId == "github").Detail, Does.Not.Contain("secret-token"));
        });
    }

    [Test]
    public async Task AMissingHostUrlIsRefusedBecauseTheTokenHasNowhereToConnect()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = Service(credentials: credentials);

        var saved = await service.SaveAsync(Save("github", "token", " "), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(409));
            Assert.That(saved.Body!.ToString(), Does.Contain("host URL"));
            Assert.That(credentials.Read("github"), Is.Null);
        });
    }

    [Test]
    public async Task ANonHttpHostUrlIsRefused()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = Service(credentials: credentials);

        var saved = await service.SaveAsync(Save("forgejo", "token", "git.example.com"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(409));
            Assert.That(saved.Body!.ToString(), Does.Contain("http"));
            Assert.That(credentials.Read("forgejo"), Is.Null);
        });
    }

    [Test]
    public async Task ATokenThatCannotReadPullRequestsIsRefusedAndNotStored()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var forge = new StubForgeCapability { TokenAccess = new ForgeTokenAccess(true, false, "The token cannot read pull requests.") };
        var service = Service(forge, credentials);

        var saved = await service.SaveAsync(Save("github", "narrow"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(409));
            Assert.That(saved.Body!.ToString(), Does.Contain("pull requests"));
            Assert.That(credentials.Read("github"), Is.Null);
        });
    }

    [Test]
    public async Task ForgettingOneHostKeepsTheOther()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges(
            new StubForgeCapability(),
            new StubForgeCapability { Identity = new("forgejo", "1.0.0", "Forgejo", "agent-up") });
        var service = new TaskSourceService(credentials, packages, new FixedTeam(new Dictionary<string, string>(), []));
        await service.SaveAsync(Save("github", "github-token"), CancellationToken.None);
        await service.SaveAsync(Save("forgejo", "forgejo-token", "https://git.example.test"), CancellationToken.None);

        service.Clear("github");

        Assert.Multiple(() =>
        {
            Assert.That(credentials.Read("github"), Is.Null);
            Assert.That(credentials.Read("forgejo")?.Token, Is.EqualTo("forgejo-token"));
            Assert.That(credentials.Read("forgejo")?.EndpointUrl, Is.EqualTo("https://git.example.test"));
        });
    }

    [Test]
    public async Task ALegacyTokenWithoutAHostIsNotConnectedAndListsNothing()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        credentials.Write("github", new TaskSourceCredential("legacy-token", ""));
        var service = Service(credentials: credentials);

        var github = service.Describe().Modules.Single(module => module.ModuleId == "github");
        var repositories = await service.ListRepositoriesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(github.Connected, Is.False);
            Assert.That(github.Detail, Does.Contain("host URL"));
            Assert.That(repositories, Is.Empty);
        });
    }

    [Test]
    public async Task AnEmptyTokenIsRefusedEvenWhenTheHostUrlIsPresent()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var saved = await Service(credentials: credentials).SaveAsync(Save("github", "  ", "https://github.com"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(409));
            Assert.That(saved.Body!.ToString(), Does.Contain("personal access token"));
            Assert.That(credentials.Read("github"), Is.Null);
        });
    }

    [Test]
    public async Task RepositoriesAreTheOnesTheStoredTokenCanSee()
    {
        var forge = new StubForgeCapability { Repositories = [new ForgeRepository("acme", "widgets")] };
        var service = Service(forge);
        await service.SaveAsync(Save("github", "token", "https://ghe.example.test"), CancellationToken.None);

        var repositories = await service.ListRepositoriesAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(repositories.Select(repository => $"{repository.ModuleId}:{repository.Owner}/{repository.Name}"), Is.EqualTo(new[] { "github:acme/widgets" }));
            Assert.That(forge.LastEndpointUrl, Is.EqualTo("https://ghe.example.test"));
        });
    }

    [Test]
    public async Task AnUnknownHostIsRefused()
    {
        var saved = await Service().SaveAsync(Save("gitlab", "token"), CancellationToken.None);

        Assert.That(saved.StatusCode, Is.EqualTo(409));
        Assert.That(saved.Body!.ToString(), Does.Contain("GitHub or Forgejo"));
    }

    [Test]
    public async Task ATokenIsRefusedUntilTheHostCapabilityIsEnabled()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = new TaskSourceService(credentials, new FakeEnabledCapabilityPackages(), new EmptyTaskSourceTeam());

        var saved = await service.SaveAsync(Save("github", "token"), CancellationToken.None);

        Assert.That(saved.StatusCode, Is.EqualTo(409));
        Assert.That(saved.Body!.ToString(), Does.Contain("Enable the GitHub"));
        Assert.That(credentials.Read("github"), Is.Null);
    }

    [Test]
    public async Task AHostThatCannotBeReachedIsReportedAsUnavailable()
    {
        var forge = new StubForgeCapability { TokenAccessFailure = new HttpRequestException("offline") };
        var saved = await Service(forge).SaveAsync(Save("github", "token"), CancellationToken.None);

        Assert.That(saved.StatusCode, Is.EqualTo(503));
        Assert.That(saved.Body!.ToString(), Does.Contain("Could not reach GitHub"));
    }

    [Test]
    public async Task ForgettingEveryHostClearsBothTokens()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = Service(credentials: credentials);
        await service.SaveAsync(Save("github", "github-token"), CancellationToken.None);

        service.Clear(null);

        Assert.That(credentials.Read("github"), Is.Null);
        Assert.That(service.Describe().Modules.Select(module => module.Connected), Is.All.False);
    }

    [Test]
    public async Task ASavedTokenWithTheCapabilityDisabledSaysToEnableIt()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        credentials.Write("github", new TaskSourceCredential("token", "https://github.com"));
        var service = new TaskSourceService(credentials, new FakeEnabledCapabilityPackages(), new EmptyTaskSourceTeam());

        var github = service.Describe().Modules.Single(module => module.ModuleId == "github");

        Assert.That(github.Connected, Is.False);
        Assert.That(github.Detail, Does.Contain("Enable the GitHub capability to use the saved token"));
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
        await service.SaveAsync(Save("github", "token"), CancellationToken.None);

        Assert.That(await service.ListRepositoriesAsync(CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task IssuesAndPullsAreEmptyWhenTheHostOrRepositoryIsMissing()
    {
        var service = Service();
        await service.SaveAsync(Save("github", "token"), CancellationToken.None);
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
        await service.SaveAsync(Save("github", "token"), CancellationToken.None);

        Assert.That(await service.ListIssuesAsync("github", "acme", "widgets", CancellationToken.None), Is.Empty);
        Assert.That(await service.ListPullsAsync("github", "acme", "widgets", CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task IssuesAndPullsAreReadFromTheNamedRepositoryAtTheSavedHost()
    {
        var forge = new StubForgeCapability
        {
            Issues = [new ForgeIssue("I-7", 7, "Cart badge", "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "https://example.test/7")],
            PullRequests =
            [
                new ForgePullRequest("42", 42, "Product cards", "https://example.test/42", ["hector"], []),
            ],
        };
        var service = Service(forge, team: new FixedTeam(new Dictionary<string, string> { ["hector"] = "devReviewer" }, ["devReviewer"]));
        await service.SaveAsync(Save("github", "token", "https://github.com"), CancellationToken.None);

        var issues = await service.ListIssuesAsync("github", "acme", "widgets", CancellationToken.None);
        var pulls = await service.ListPullsAsync("github", "acme", "widgets", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(issues.Single().Number, Is.EqualTo(7));
            Assert.That(pulls.Single().Reviews.Single().Standing, Is.EqualTo("reviewing"));
            Assert.That(pulls.Single().Reviews.Single().Kind, Is.EqualTo("devReviewer"));
            Assert.That(forge.LastEndpointUrl, Is.EqualTo("https://github.com"));
        });
    }

    private static SaveTaskSourceTokenRequest Save(string moduleId, string token, string endpoint = "https://github.com")
        => new(moduleId, endpoint, token);

    private static TaskSourceService Service(
        StubForgeCapability? forge = null,
        MemoryTaskSourceCredentialStore? credentials = null,
        FixedTeam? team = null)
    {
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges(forge ?? new StubForgeCapability());
        return new TaskSourceService(credentials ?? new MemoryTaskSourceCredentialStore(), packages, team ?? new FixedTeam(new Dictionary<string, string>(), []));
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
