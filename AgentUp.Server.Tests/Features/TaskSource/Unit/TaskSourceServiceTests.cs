using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Providers;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.TaskSource.Unit;

[TestFixture]
public sealed class TaskSourceServiceTests
{
    [Test]
    public void AServerNobodyHasPointedAnywhereSaysSo()
    {
        var described = Service().Describe();

        Assert.Multiple(() =>
        {
            Assert.That(described.Configured, Is.False);
            Assert.That(described.Connected, Is.False);
            Assert.That(described.Detail, Does.Contain("Name the repository"));
            Assert.That(described.Modules.Single().ModuleId, Is.EqualTo("github"));
        });
    }

    [Test]
    public void NamingARepositoryKeepsItAndAsksForASignIn()
    {
        var service = Service();

        var described = service.Set(new SetTaskSourceRequest("github", " agent-up-oss ", " agent-up "));

        Assert.Multiple(() =>
        {
            Assert.That(described.Configured, Is.True);
            Assert.That(described.Owner, Is.EqualTo("agent-up-oss"), "surrounding space is not part of a name");
            Assert.That(described.Repository, Is.EqualTo("agent-up"));
            Assert.That(described.Connected, Is.False);
            Assert.That(described.CanConnect, Is.True);
            Assert.That(described.Detail, Does.Contain("Sign in"));
        });
    }

    [Test]
    public void AnIncompleteRepositoryIsNotKept()
    {
        var service = Service();

        var described = service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "   "));

        Assert.That(described.Configured, Is.False);
    }

    [Test]
    public void SigningInSendsTheReaderToTheHostWithAStateAndTheConfiguredClient()
    {
        var service = Service();
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));

        var outcome = service.StartLogin();
        var url = ((TaskSourceLoginDto)outcome.Body!).AuthorizeUrl;

        Assert.Multiple(() =>
        {
            Assert.That(outcome.StatusCode, Is.EqualTo(200));
            Assert.That(url, Does.StartWith("https://example.test/login/oauth/authorize?"));
            Assert.That(url, Does.Contain("client_id=client"));
            Assert.That(url, Does.Contain("redirect_uri=https%3A%2F%2Fexample.test%2Fcallback"));
            Assert.That(url, Does.Contain("state="));
            Assert.That(url, Does.Contain("response_type=code"));
        });
    }

    [Test]
    public void SigningInBeforeNamingARepositoryIsRefusedRatherThanGuessed()
    {
        var outcome = Service().StartLogin();

        Assert.That(outcome.StatusCode, Is.EqualTo(409));
    }

    [Test]
    public void SigningInWithoutAnOAuthClientSaysThatIsWhatIsMissing()
    {
        var service = Service(oauth: new ForgeOAuthConfiguration("", "", ""));
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));

        var outcome = service.StartLogin();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.StatusCode, Is.EqualTo(503));
            Assert.That(service.Describe().CanConnect, Is.False);
        });
    }

    [Test]
    public async Task TheCallbackExchangesTheCodeAndTheSourceReadsAsConnected()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = Service(credentials: credentials);
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));
        var state = StateFrom(service.StartLogin());

        var connected = await service.CompleteLoginAsync("code", state, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(connected, Is.True);
            Assert.That(credentials.Read("github"), Is.EqualTo("exchanged"));
            Assert.That(service.Describe().Connected, Is.True);
            Assert.That(service.Describe().Detail, Does.Contain("Reading issues from agent-up-oss/agent-up"));
        });
    }

    [Test]
    public async Task ACallbackReplayedWithTheSameStateIsRefused()
    {
        var service = Service();
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));
        var state = StateFrom(service.StartLogin());

        await service.CompleteLoginAsync("code", state, CancellationToken.None);

        Assert.That(await service.CompleteLoginAsync("code", state, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task ACallbackCarryingAStateNobodyIssuedIsRefused()
        => Assert.That(
            await Service().CompleteLoginAsync("code", "invented", CancellationToken.None),
            Is.False);

    [Test]
    public async Task AHostThatWillNotExchangeTheCodeLeavesTheSourceUnconnected()
    {
        var service = Service(exchange: new ThrowingForgeOAuthExchange(new HttpRequestException("refused")));
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));
        var state = StateFrom(service.StartLogin());

        var connected = await service.CompleteLoginAsync("code", state, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(connected, Is.False);
            Assert.That(service.Describe().Connected, Is.False);
        });
    }

    [Test]
    public async Task TheIssuesComeFromTheConfiguredRepositoryOnceItIsConnected()
    {
        var issue = new ForgeIssue("7", 3, "Restock", "do it", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "u");
        var forge = new StubForgeCapability { Issues = [issue] };
        var service = Service(forge: forge);
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));
        await service.CompleteLoginAsync("code", StateFrom(service.StartLogin()), CancellationToken.None);

        var issues = await service.ListIssuesAsync(CancellationToken.None);

        Assert.That(issues.Single().Number, Is.EqualTo(3));
    }

    [Test]
    public async Task AnUnconnectedSourceOffersNothingRatherThanFailing()
    {
        var service = Service();
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));

        Assert.That(await service.ListIssuesAsync(CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task AHostThatCannotBeReachedOffersNothingRatherThanFailing()
    {
        var forge = new StubForgeCapability { ListIssuesFailure = new HttpRequestException("offline") };
        var service = Service(forge: forge);
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));
        await service.CompleteLoginAsync("code", StateFrom(service.StartLogin()), CancellationToken.None);

        Assert.That(await service.ListIssuesAsync(CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task PointingTheServerAtAnotherHostDropsTheTokenItHeldForTheOldOne()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = Service(
            credentials: credentials,
            forges: [new StubForgeCapability(), Forgejo()]);
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));
        await service.CompleteLoginAsync("code", StateFrom(service.StartLogin()), CancellationToken.None);

        service.Set(new SetTaskSourceRequest("forgejo", "acme", "widgets"));

        Assert.Multiple(() =>
        {
            Assert.That(credentials.Read("github"), Is.Null, "a credential is for one host");
            Assert.That(service.Describe().Connected, Is.False);
        });
    }

    [Test]
    public async Task ForgettingTheSourceAlsoForgetsTheToken()
    {
        var credentials = new MemoryTaskSourceCredentialStore();
        var service = Service(credentials: credentials);
        service.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));
        await service.CompleteLoginAsync("code", StateFrom(service.StartLogin()), CancellationToken.None);

        var described = service.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(described.Configured, Is.False);
            Assert.That(credentials.Read("github"), Is.Null);
        });
    }

    [Test]
    public void AServerWithNoRepositoryHostEnabledSaysToEnableOne()
    {
        var described = Service(forges: []).Describe();

        Assert.Multiple(() =>
        {
            Assert.That(described.Modules, Is.Empty);
            Assert.That(described.Detail, Does.Contain("No repository host is enabled"));
        });
    }

    /// <summary>A host that will not exchange the code, which is a refusal rather than a crash.</summary>
    private sealed class ThrowingForgeOAuthExchange(Exception failure) : IForgeOAuthExchange
    {
        public Task<string> ExchangeAsync(string code, string tokenUrl, CancellationToken cancellationToken)
            => Task.FromException<string>(failure);
    }

    private static string StateFrom(TaskSourceOutcome outcome)
    {
        var url = ((TaskSourceLoginDto)outcome.Body!).AuthorizeUrl;
        return System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["state"]!;
    }

    private static StubForgeCapability Forgejo()
        => new()
        {
            Identity = new AgentUp.Sdk.Common.CapabilityIdentity("forgejo", "1.0.0", "Forgejo", "agent-up")
        };

    private static TaskSourceService Service(
        StubForgeCapability? forge = null,
        IReadOnlyList<IForgeCapability>? forges = null,
        ITaskSourceCredentialStore? credentials = null,
        ForgeOAuthConfiguration? oauth = null,
        IForgeOAuthExchange? exchange = null)
    {
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges([.. forges ?? [forge ?? new StubForgeCapability()]]);
        return new TaskSourceService(
            new MemoryTaskSourceStore(),
            credentials ?? new MemoryTaskSourceCredentialStore(),
            new TaskSourceStateStore(),
            packages,
            new FixedForgeOAuthSettings(
                oauth ?? new ForgeOAuthConfiguration("client", "secret", "https://example.test/callback")),
            exchange ?? new FixedForgeOAuthExchange("exchanged"));
    }
}
