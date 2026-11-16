using System.Reactive.Linq;
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
    public async Task AServerNobodyHasPointedAnywhereOffersToSetOne()
    {
        var source = ViewModel(new FakeTaskSourceApi());

        await source.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(source.Label, Is.EqualTo("Not set"));
            Assert.That(source.Configured, Is.False);
            Assert.That(source.EditLabel, Is.EqualTo("Set repository"));
            Assert.That(source.ShowsConnect, Is.False, "there is nothing to sign in to yet");
        });
    }

    [Test]
    public async Task AConfiguredButUnconnectedSourceOffersToConnect()
    {
        var source = ViewModel(new FakeTaskSourceApi { State = Source(connected: false) });

        await source.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(source.Label, Is.EqualTo("agent-up-oss/agent-up"));
            Assert.That(source.ShowsConnect, Is.True);
            Assert.That(source.EditLabel, Is.EqualTo("Change"));
        });
    }

    [Test]
    public async Task ASourceThatCannotStartASignInDoesNotOfferOne()
    {
        var source = ViewModel(new FakeTaskSourceApi { State = Source(connected: false, canConnect: false) });

        await source.LoadAsync();

        Assert.That(source.ShowsConnect, Is.False);
    }

    [Test]
    public async Task SavingTakesTheRepositoryHoweverTheReaderWroteIt()
    {
        var api = new FakeTaskSourceApi { State = Source() };
        var source = ViewModel(api);
        await source.LoadAsync();

        source.EditCommand.Execute().Subscribe();
        source.Typed = "https://github.com/acme/widgets.git";
        await source.SaveCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(api.Saved, Is.EqualTo(("github", "acme", "widgets")));
            Assert.That(source.Editing, Is.False);
        });
    }

    [Test]
    public async Task SomethingThatIsNotARepositoryCannotBeSaved()
    {
        var api = new FakeTaskSourceApi();
        var source = ViewModel(api);
        await source.LoadAsync();

        source.EditCommand.Execute().Subscribe();
        source.Typed = "agent-up";
        await source.SaveCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(source.CanSave, Is.False);
            Assert.That(api.Saved, Is.Null, "a half-named source would point the Server at nothing");
        });
    }

    [Test]
    public async Task AMachineWithNoBrowserToOpenSaysSoRatherThanDoingNothingVisible()
    {
        var api = new FakeTaskSourceApi { State = Source(connected: false) };
        var source = ViewModel(api, _ => false);
        await source.LoadAsync();

        await source.ConnectCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(source.HasError, Is.True);
            Assert.That(source.Detail, Does.Contain("Could not open a browser"));
        });
    }

    [Test]
    public async Task ConnectingHandsTheAuthorizePageToTheBrowser()
    {
        var opened = new List<string>();
        var api = new FakeTaskSourceApi { State = Source(connected: false), AuthorizeUrl = "https://example.test/authorize" };
        var source = ViewModel(api, url => { opened.Add(url); return true; });
        await source.LoadAsync();

        await source.ConnectCommand.Execute();

        Assert.That(opened.Single(), Is.EqualTo("https://example.test/authorize"));
    }

    [Test]
    public async Task AServerThatCannotStartTheSignInSaysSoRatherThanOpeningNothing()
    {
        var opened = new List<string>();
        var api = new FakeTaskSourceApi { State = Source(connected: false), AuthorizeUrl = null };
        var source = ViewModel(api, url => { opened.Add(url); return true; });
        await source.LoadAsync();

        await source.ConnectCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(opened, Is.Empty);
            Assert.That(source.HasError, Is.True);
            Assert.That(source.Detail, Does.Contain("could not start a sign-in"));
        });
    }

    [Test]
    public async Task ForgettingTakesTheSourceBackOut()
    {
        var api = new FakeTaskSourceApi { State = Source() };
        var source = ViewModel(api);
        await source.LoadAsync();

        await source.ForgetCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(api.Cleared, Is.True);
            Assert.That(source.Configured, Is.False);
        });
    }

    [Test]
    public async Task AServerThatCannotBeReachedSaysWhatWentWrong()
    {
        var source = ViewModel(new FakeTaskSourceApi { Failure = new HttpRequestException("no route") });

        await source.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(source.HasError, Is.True);
            Assert.That(source.Detail, Is.EqualTo("no route"));
        });
    }

    [Test]
    public void ARepositoryIsTakenHoweverItWasWritten()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaskSourceService.Split("agent-up-oss/agent-up"), Is.EqualTo(("agent-up-oss", "agent-up")));
            Assert.That(TaskSourceService.Split("  agent-up-oss/agent-up "), Is.EqualTo(("agent-up-oss", "agent-up")));
            Assert.That(
                TaskSourceService.Split("https://github.com/agent-up-oss/agent-up"),
                Is.EqualTo(("agent-up-oss", "agent-up")));
            Assert.That(
                TaskSourceService.Split("https://github.com/agent-up-oss/agent-up.git"),
                Is.EqualTo(("agent-up-oss", "agent-up")));
            Assert.That(TaskSourceService.Split("agent-up"), Is.Null);
            Assert.That(TaskSourceService.Split("   "), Is.Null);
        });
    }

    private static TaskSource Source(bool connected = true, bool canConnect = true)
        => new(
            "github",
            "agent-up-oss",
            "agent-up",
            Configured: true,
            Connected: connected,
            CanConnect: canConnect,
            Detail: connected ? "Reading issues." : "Sign in to read issues.",
            Modules: [new TaskSourceModule("github", "GitHub")]);

    private static TaskSourceViewModel ViewModel(ITaskSourceApiProvider api, Func<string, bool>? open = null)
        => new(new TaskSourceController(new TaskSourceService(api)), open ?? (_ => true));

    private sealed class FakeTaskSourceApi : ITaskSourceApiProvider
    {
        public TaskSource? State { get; set; }

        public string? AuthorizeUrl { get; init; } = "https://example.test/authorize";

        public Exception? Failure { get; init; }

        public (string Module, string Owner, string Repository)? Saved { get; private set; }

        public bool Cleared { get; private set; }

        public Task<TaskSource?> ReadAsync(CancellationToken cancellationToken)
            => Failure is null ? Task.FromResult(State) : Task.FromException<TaskSource?>(Failure);

        public Task<TaskSource?> SetAsync(
            string moduleId,
            string owner,
            string repository,
            CancellationToken cancellationToken)
        {
            Saved = (moduleId, owner, repository);
            State = Source() with { Owner = owner, Repository = repository };
            return Task.FromResult(State);
        }

        public Task<TaskSource?> ClearAsync(CancellationToken cancellationToken)
        {
            Cleared = true;
            State = null;
            return Task.FromResult(State);
        }

        public Task<string?> StartLoginAsync(CancellationToken cancellationToken)
            => Task.FromResult(AuthorizeUrl);
    }
}
