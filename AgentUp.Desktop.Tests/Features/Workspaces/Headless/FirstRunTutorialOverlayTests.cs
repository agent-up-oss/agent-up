using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public class FirstRunTutorialOverlayTests
{
    [AvaloniaTest]
    public async Task FirstRunTutorialOverlay_isVisible_whenTutorialIsRequired()
    {
        var tutorial = new FirstRunTutorialViewModel(
            new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(false, false, 0)),
            new PassingTutorialChecks());

        var app = await AppDriver.LaunchEmptyAsync(tutorial);

        Assert.That(app.Content.ShowsFirstRunTutorial, Is.True);
    }

    [AvaloniaTest]
    public async Task SkipTutorialButton_hidesOverlay()
    {
        var store = new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(false, false, 0));
        var tutorial = new FirstRunTutorialViewModel(store, new PassingTutorialChecks());
        var app = await AppDriver.LaunchEmptyAsync(tutorial);

        await app.Content.ClickSkipTutorialAsync();

        Assert.That(app.Content.ShowsFirstRunTutorial, Is.False);
        Assert.That(store.Settings.TutorialSkipped, Is.True);
    }

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
