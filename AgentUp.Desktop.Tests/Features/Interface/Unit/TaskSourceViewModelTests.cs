using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Interface.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

[TestFixture]
public sealed class TaskSourceViewModelTests
{
    [Test]
    public async Task LoadAsync_showsWhichHostsAreConnected()
    {
        var source = ViewModel(new FakeTaskSourceApi { State = Connected() });

        await source.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(source.IsConnected("github"), Is.True);
            Assert.That(source.Error, Is.Null);
            Assert.That(source.DetailFor("github"), Does.Contain("GitHub"));
            Assert.That(source.EndpointFor("github"), Is.EqualTo("https://github.com"));
            Assert.That(source.IsConnected("forgejo"), Is.False);
            Assert.That(source.EndpointFor("forgejo"), Is.EqualTo(""));
        });
    }

    [Test]
    public async Task SaveAsync_sendsTheTokenAndDoesNotKeepIt()
    {
        var api = new FakeTaskSourceApi { State = Connected(false) };
        var source = ViewModel(api);

        await source.SaveAsync("github", " https://github.com ", " secret-token ");

        Assert.Multiple(() =>
        {
            Assert.That(api.Saved, Is.EqualTo(("github", "https://github.com", "secret-token")));
            Assert.That(source.DetailFor("github"), Does.Not.Contain("secret-token"));
            Assert.That(source.IsConnected("github"), Is.True);
            Assert.That(source.EndpointFor("github"), Is.EqualTo("https://github.com"));
        });
    }

    [Test]
    public async Task SaveAsync_showsTheServersReasonWhenTheTokenIsRefused()
    {
        var source = ViewModel(new FakeTaskSourceApi { Failure = new HttpRequestException("The token cannot read pull requests.") });

        await source.SaveAsync("github", "https://github.com", "narrow");

        Assert.Multiple(() =>
        {
            Assert.That(source.HasError, Is.True);
            Assert.That(source.DetailFor("github"), Does.Contain("pull requests"));
        });
    }

    [Test]
    public async Task ForgetAsync_dropsThatHost()
    {
        var api = new FakeTaskSourceApi { State = Connected() };
        var source = ViewModel(api);
        await source.LoadAsync();

        await source.ForgetAsync("github");

        Assert.Multiple(() =>
        {
            Assert.That(api.Forgotten, Is.EqualTo("github"));
            Assert.That(source.IsConnected("github"), Is.False);
        });
    }

    [Test]
    public async Task LoadAsync_showsTheServersReasonWhenTheReadFails()
    {
        var source = ViewModel(new FakeTaskSourceApi { Failure = new HttpRequestException("offline") });

        await source.LoadAsync();

        Assert.That(source.HasError, Is.True);
        Assert.That(source.DetailFor("github"), Does.Contain("offline"));
    }

    [Test]
    public async Task ForgetAsync_showsTheServersReasonWhenTheHostCannotBeForgotten()
    {
        var source = ViewModel(new FakeTaskSourceApi { Failure = new HttpRequestException("offline") });

        await source.ForgetAsync("github");

        Assert.That(source.HasError, Is.True);
        Assert.That(source.DetailFor("github"), Does.Contain("offline"));
    }

    [Test]
    public async Task LoadRepositoriesAsync_showsTheServersReasonWhenTheListFails()
    {
        var source = ViewModel(new FakeTaskSourceApi { RepositoryFailure = new HttpRequestException("offline") });

        await source.LoadRepositoriesAsync();

        Assert.That(source.HasError, Is.True);
        Assert.That(source.DetailFor("github"), Does.Contain("offline"));
    }

    [Test]
    public void DetailFor_fallsBackWhenNoHostIsLoaded()
    {
        var source = ViewModel(new FakeTaskSourceApi());

        Assert.That(source.DetailFor("github"), Does.Contain("host URL"));
        Assert.That(source.EndpointFor("github"), Is.EqualTo("https://github.com"));
        Assert.That(source.IsConnected("github"), Is.False);
    }

    [Test]
    public async Task LoadAsync_showsTheServersReasonWhenTheReadTimesOut()
    {
        var source = ViewModel(new FakeTaskSourceApi { Failure = new TaskCanceledException("timed out") });

        await source.LoadAsync();

        Assert.That(source.HasError, Is.True);
        Assert.That(source.DetailFor("github"), Does.Contain("timed out"));
    }

    [Test]
    public async Task PullsAsync_asksTheNamedRepository()
    {
        var pulls = new[] { new TaskSourcePull("42", 42, "Product cards", "u", []) };
        var source = ViewModel(new FakeTaskSourceApi { Pulls = pulls });

        var listed = await source.PullsAsync("github", "example", "repository");

        Assert.That(listed.Single().Number, Is.EqualTo(42));
    }

    [Test]
    public async Task RepositoriesLoadFromTheStoredTokens()
    {
        var source = ViewModel(new FakeTaskSourceApi
        {
            State = Connected(),
            Repositories = [new TaskSourceRepository("github", "example", "repository")],
        });

        await source.LoadRepositoriesAsync();

        Assert.That(source.Repositories.Single().Name, Is.EqualTo("repository"));
    }

    private static TaskSource Connected(bool github = true) => new(
    [
        new TaskSourceModule("github", "GitHub", github, github ? "Reading repositories and pull requests from GitHub." : "Paste a token.", github ? "https://github.com" : null),
        new TaskSourceModule("forgejo", "Forgejo", false, "Enable the Forgejo capability, then paste a personal access token."),
    ]);

    private static TaskSourceViewModel ViewModel(ITaskSourceApiProvider api)
        => new(new TaskSourceController(new TaskSourceService(api)));

    private sealed class FakeTaskSourceApi : ITaskSourceApiProvider
    {
        public TaskSource? State { get; set; }

        public IReadOnlyList<TaskSourceRepository> Repositories { get; init; } = [];

        public IReadOnlyList<TaskSourcePull> Pulls { get; init; } = [];

        public Exception? Failure { get; init; }

        public Exception? RepositoryFailure { get; init; }

        public (string Module, string Endpoint, string Token)? Saved { get; private set; }

        public string? Forgotten { get; private set; }

        public Task<TaskSource?> ReadAsync(CancellationToken cancellationToken)
            => Failure is null ? Task.FromResult(State) : Task.FromException<TaskSource?>(Failure);

        public Task<TaskSource?> SaveAsync(string moduleId, string endpointUrl, string token, CancellationToken cancellationToken)
        {
            if (Failure is not null)
                return Task.FromException<TaskSource?>(Failure);
            Saved = (moduleId, endpointUrl, token);
            State = Connected();
            return Task.FromResult<TaskSource?>(State);
        }

        public Task<TaskSource?> ForgetAsync(string moduleId, CancellationToken cancellationToken)
        {
            if (Failure is not null)
                return Task.FromException<TaskSource?>(Failure);
            Forgotten = moduleId;
            State = Connected(false);
            return Task.FromResult<TaskSource?>(State);
        }

        public Task<IReadOnlyList<TaskSourceRepository>> ListRepositoriesAsync(CancellationToken cancellationToken)
            => RepositoryFailure is not null
                ? Task.FromException<IReadOnlyList<TaskSourceRepository>>(RepositoryFailure)
                : Task.FromResult(Repositories);

        public Task<IReadOnlyList<TaskSourcePull>> ListPullsAsync(
            string moduleId,
            string owner,
            string repository,
            CancellationToken cancellationToken)
            => Task.FromResult(Pulls);
    }
}
