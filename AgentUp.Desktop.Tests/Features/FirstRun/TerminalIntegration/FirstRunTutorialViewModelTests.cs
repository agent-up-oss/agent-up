using System.Reactive.Linq;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;

namespace AgentUp.Desktop.Tests.Features.FirstRun.TerminalIntegration;

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
    public async Task InitializeAsync_cleansOldTutorialWorkspaces_whenTutorialStarts()
    {
        var checks = new CountingTutorialChecks();
        var vm = CreateViewModel(new FirstRunTutorialSettings(false, false, 5), checks: checks);

        await vm.InitializeAsync();

        Assert.That(checks.CleanupCount, Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_doesNotCleanTutorialWorkspaces_whenTutorialIsHidden()
    {
        var checks = new CountingTutorialChecks();
        var vm = CreateViewModel(new FirstRunTutorialSettings(true, false, 7), checks: checks);

        await vm.InitializeAsync();

        Assert.That(checks.CleanupCount, Is.EqualTo(0));
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
        await vm.CreateSampleProjectCommand.Execute().FirstAsync();
        await vm.CheckProjectFilesCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        await vm.CreateAgentUpJsonCommand.Execute().FirstAsync();
        await vm.CheckAgentUpJsonCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        await vm.StartWorkspaceCommand.Execute().FirstAsync();
        await vm.CheckWorkspaceCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
        await vm.MarkDuplicateStartedCommand.Execute().FirstAsync();
        await vm.CheckDuplicateCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();
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

    [Test]
    public async Task CreateSampleProjectCommand_revealsDirectoryTree_andProjectFileCheckSection()
    {
        var root = Path.Join(Path.GetTempPath(), $"agent-up-vm-tree-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var vm = CreateViewModel(checks: new FileCreatingTutorialChecks());
            await vm.InitializeAsync();
            vm.ProjectDirectory = root;
            await vm.CreateSampleProjectCommand.Execute().FirstAsync();

            Assert.That(vm.ProjectDirectory, Is.EqualTo(root));
            Assert.That(vm.ShowProjectFileCheckSection, Is.True);
            Assert.That(vm.ShowDirectoryTree, Is.True);
            Assert.That(vm.DirectoryTree, Does.Contain("docker-compose.yaml"));
            Assert.That(vm.DirectoryTree, Does.Contain("web/"));
            Assert.That(vm.DirectoryTree, Does.Contain("api/"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task BackCommand_movesToPreviousStep_andRequiresStepToBeCompletedAgain()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        await vm.CheckDockerCommand.Execute().FirstAsync();
        await vm.ContinueCommand.Execute().FirstAsync();

        Assert.That(vm.CurrentStep, Is.EqualTo(2));

        vm.BackCommand.Execute().Subscribe();

        Assert.That(vm.CurrentStep, Is.EqualTo(1));
        Assert.That(vm.CanContinue, Is.False);
    }

    [Test]
    public async Task StartWorkspaceCommand_showsOutput_butDoesNotRevealWorkspaceCheck_whenCommandFails()
    {
        var vm = CreateViewModel(checks: new FailingStartTutorialChecks());
        await vm.InitializeAsync();
        vm.ProjectDirectory = "/tmp/example";

        await vm.StartWorkspaceCommand.Execute().FirstAsync();

        Assert.That(vm.ShowCommandOutput, Is.True);
        Assert.That(vm.CommandOutput, Does.Contain("start failed"));
        Assert.That(vm.ShowWorkspaceCheckSection, Is.False);
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

    private class PassingTutorialChecks : IFirstRunTutorialChecks
    {
        public virtual Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public virtual Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Docker works."));

        public virtual Task<FirstRunCheckResult> CheckNodeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Node works."));

        public virtual Task<FirstRunSampleProjectResult> CreateJavaScriptSampleAsync(string? currentProjectDirectory = null, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunSampleProjectResult.Success("Sample created.", currentProjectDirectory ?? "/tmp/tutorial/agent-up-tutorial/example-agent1"));

        public virtual Task<FirstRunCheckResult> CheckJavaScriptProjectFilesAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Project files work."));

        public virtual Task<FirstRunCheckResult> CreateAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("agent-up.json created."));

        public virtual Task<FirstRunCheckResult> CheckAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("agent-up.json works."));

        public virtual Task<FirstRunCheckResult> StartJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Started."));

        public virtual Task<FirstRunCheckResult> CheckJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Workspace works."));

        public virtual Task<FirstRunCheckResult> CreateDuplicatedJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Duplicate created."));

        public virtual Task<FirstRunCheckResult> CheckDuplicatedJavaScriptWorkspacesAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Duplicate works."));
    }

    private sealed class FileCreatingTutorialChecks : PassingTutorialChecks
    {
        public override Task<FirstRunSampleProjectResult> CreateJavaScriptSampleAsync(string? currentProjectDirectory = null, CancellationToken cancellationToken = default)
        {
            var projectDirectory = currentProjectDirectory ?? Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "agent-up-tutorial", "example-agent1");
            Directory.CreateDirectory(Path.Join(projectDirectory, "web"));
            Directory.CreateDirectory(Path.Join(projectDirectory, "api"));
            File.WriteAllText(Path.Join(projectDirectory, "docker-compose.yaml"), "services: {}");
            File.WriteAllText(Path.Join(projectDirectory, "web", "package.json"), "{}");
            File.WriteAllText(Path.Join(projectDirectory, "api", "package.json"), "{}");
            return Task.FromResult(FirstRunSampleProjectResult.Success("Sample created.", projectDirectory));
        }
    }

    private sealed class FailingStartTutorialChecks : PassingTutorialChecks
    {
        public override Task<FirstRunCheckResult> StartJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Failure("start failed\nstderr:\nboom"));
    }

    private sealed class CountingTutorialChecks : PassingTutorialChecks
    {
        public int CleanupCount { get; private set; }

        public override Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
        {
            CleanupCount++;
            return Task.CompletedTask;
        }
    }
}
