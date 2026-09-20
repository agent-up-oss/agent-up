using AgentUp.Desktop.Features.FirstRun.Controllers;
using AgentUp.Desktop.Features.FirstRun.Interfaces;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;

namespace AgentUp.Desktop.Tests.Features.FirstRun.Controller;

[TestFixture]
public sealed class FirstRunControllerTests
{
    [Test]
    public async Task InitializeAsync_shows_an_incomplete_tutorial_and_runs_cleanup()
    {
        var checks = new RecordingChecks();
        var tutorial = new FirstRunTutorialViewModel(
            new StaticSettingsStore(new FirstRunTutorialSettings(false, false, 0)), checks);

        await new FirstRunController(tutorial).InitializeAsync();

        Assert.That(tutorial.IsVisible, Is.True);
        Assert.That(checks.CleanupCalled, Is.True);
    }

    [Test]
    public async Task InitializeAsync_keeps_a_completed_tutorial_hidden_without_cleanup()
    {
        var checks = new RecordingChecks();
        var tutorial = new FirstRunTutorialViewModel(
            new StaticSettingsStore(new FirstRunTutorialSettings(true, false, 7)), checks);

        await new FirstRunController(tutorial).InitializeAsync();

        Assert.That(tutorial.IsVisible, Is.False);
        Assert.That(checks.CleanupCalled, Is.False);
    }

    private sealed class StaticSettingsStore(FirstRunTutorialSettings settings) : IFirstRunTutorialSettingsStore
    {
        public Task<FirstRunTutorialSettings> LoadAsync() => Task.FromResult(settings);
        public Task SaveAsync(FirstRunTutorialSettings value) => Task.CompletedTask;
    }

    private sealed class RecordingChecks : IFirstRunTutorialChecks
    {
        public bool CleanupCalled { get; private set; }
        public Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
        {
            CleanupCalled = true;
            return Task.CompletedTask;
        }

        public Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default) => Unused();
        public Task<FirstRunCheckResult> CheckNodeAsync(CancellationToken cancellationToken = default) => Unused();
        public Task<FirstRunSampleProjectResult> CreateJavaScriptSampleAsync(string? currentProjectDirectory = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FirstRunCheckResult> CheckJavaScriptProjectFilesAsync(string projectDirectory, CancellationToken cancellationToken = default) => Unused();
        public Task<FirstRunCheckResult> CreateAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default) => Unused();
        public Task<FirstRunCheckResult> CheckAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default) => Unused();
        public Task<FirstRunCheckResult> StartJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default) => Unused();
        public Task<FirstRunCheckResult> CheckJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default) => Unused();
        public Task<FirstRunCheckResult> CreateDuplicatedJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default) => Unused();
        public Task<FirstRunCheckResult> CheckDuplicatedJavaScriptWorkspacesAsync(string projectDirectory, CancellationToken cancellationToken = default) => Unused();

        private static Task<FirstRunCheckResult> Unused()
            => Task.FromResult(FirstRunCheckResult.Failure("unused"));
    }
}
