using System.Reactive.Linq;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;

namespace AgentUp.Desktop.Tests.Features.FirstRun.Unit;

[TestFixture]
public class FirstRunTutorialViewModelTests
{
    [Test]
    public async Task InitializeAsync_showsTutorial_whenNoCompletionStateExists()
    {
        var vm = CreateViewModel(new FirstRunTutorialSettings(false, false, 0));

        await vm.InitializeAsync();

        Assert.That(vm.IsVisible, Is.True);
        Assert.That(vm.CurrentStep, Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_hidesTutorial_whenAlreadyCompleted()
    {
        var vm = CreateViewModel(new FirstRunTutorialSettings(true, false, 7));

        await vm.InitializeAsync();

        Assert.That(vm.IsVisible, Is.False);
    }

    [Test]
    public async Task ContinueCommand_doesNotAdvance_untilDockerCheckSucceeds()
    {
        var vm = CreateViewModel(new FirstRunTutorialSettings(false, false, 0));
        await vm.InitializeAsync();

        await vm.ContinueCommand.Execute().FirstAsync();

        Assert.That(vm.CurrentStep, Is.EqualTo(1));
    }

    [Test]
    public async Task DockerCheck_success_enablesProgressToEnvironmentStep()
    {
        var store = new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(false, false, 0));
        var vm = CreateViewModel(store: store);
        await vm.InitializeAsync();

        await vm.CheckDockerCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();

        Assert.That(vm.CurrentStep, Is.EqualTo(2));
        Assert.That(store.Settings.CompletedStep, Is.EqualTo(1));
    }

    [Test]
    public async Task JavaScriptPath_requiresNode_workspace_andDuplicateChecks_beforeCompleting()
    {
        var store = new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(false, false, 0));
        var vm = CreateViewModel(store: store);
        await vm.InitializeAsync();

        await vm.CheckDockerCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        await vm.SelectJavaScriptCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        await vm.CheckNodeCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        vm.ProjectDirectory = "/tmp/example";
        await vm.CreateSampleProjectCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        await vm.CreateAgentUpJsonCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        await vm.StartWorkspaceCommand.Execute().FirstAsync();
        await vm.CheckWorkspaceCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        await vm.CheckDuplicateCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();

        Assert.That(vm.IsVisible, Is.False);
        Assert.That(store.Settings.TutorialCompleted, Is.True);
        Assert.That(store.Settings.CompletedStep, Is.EqualTo(7));
    }

    [Test]
    public async Task SkipCommand_hidesTutorial_andPersistsSkipState()
    {
        var store = new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(false, false, 0));
        var vm = CreateViewModel(store: store);
        await vm.InitializeAsync();

        await vm.SkipCommand.Execute().FirstAsync();

        Assert.That(vm.IsVisible, Is.False);
        Assert.That(store.Settings.TutorialSkipped, Is.True);
    }

    private static FirstRunTutorialViewModel CreateViewModel(
        FirstRunTutorialSettings? settings = null,
        InMemoryTutorialSettingsStore? store = null,
        IFirstRunTutorialChecks? checks = null)
        => new(
            store ?? new InMemoryTutorialSettingsStore(settings ?? new FirstRunTutorialSettings(false, false, 0)),
            checks ?? new PassingTutorialChecks());

    private sealed class InMemoryTutorialSettingsStore(FirstRunTutorialSettings settings) : IFirstRunTutorialSettingsStore
    {
        public FirstRunTutorialSettings Settings { get; private set; } = settings;

        public Task<FirstRunTutorialSettings> LoadAsync() => Task.FromResult(Settings);

        public Task SaveAsync(FirstRunTutorialSettings settings)
        {
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class PassingTutorialChecks : IFirstRunTutorialChecks
    {
        public Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Docker works."));

        public Task<FirstRunCheckResult> CheckNodeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Node works."));

        public Task<FirstRunCheckResult> CreateJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Sample created."));

        public Task<FirstRunCheckResult> CheckJavaScriptProjectFilesAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Project files work."));

        public Task<FirstRunCheckResult> CreateAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("agent-up.json created."));

        public Task<FirstRunCheckResult> CheckAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("agent-up.json works."));

        public Task<FirstRunCheckResult> StartJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Started."));

        public Task<FirstRunCheckResult> CheckJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Workspace works."));

        public Task<FirstRunCheckResult> CheckDuplicatedJavaScriptWorkspacesAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Duplicate works."));
    }
}
