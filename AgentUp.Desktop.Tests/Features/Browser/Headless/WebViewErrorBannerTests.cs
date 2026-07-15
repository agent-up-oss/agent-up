using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.FirstRun.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Browser.Headless;

[TestFixture]
public sealed class WebViewErrorBannerTests
{
    [AvaloniaTest]
    public async Task PortPane_showsErrorBanner_whenWebViewCreationFails()
    {
        var ws = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            ws,
            () => throw new InvalidOperationException("no WebKit installed"));

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        Assert.That(app.Content.PortPaneShowsError, Is.True);
        Assert.That(app.Content.WebViewErrorMessage, Does.Contain("no WebKit installed"));
    }

    [AvaloniaTest]
    public async Task PortPane_hidesBanner_whenSwitchingToWorkspaceWithoutError()
    {
        var ws1 = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var ws2 = WorkspaceFixtures.WithHttpPort("ws-2", 4000);
        var app = await AppDriver.LaunchWithWorkspacesAsync(
            [ws1, ws2],
            () => throw new InvalidOperationException("no WebKit"));

        // Force an error on ws-1.
        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();
        Assert.That(app.Content.PortPaneShowsError, Is.True);

        // Switch to ws-2 (no error recorded for it) — banner must hide.
        app.Window.NavigateTo("ws-2", null);
        await HeadlessExtensions.FlushAsync();
        Assert.That(app.Content.PortPaneShowsError, Is.False);
    }

    [AvaloniaTest]
    public async Task PortPane_reShowsBanner_whenSwitchingBackToFailedWorkspace()
    {
        var ws1 = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var ws2 = WorkspaceFixtures.WithHttpPort("ws-2", 4000);
        var app = await AppDriver.LaunchWithWorkspacesAsync(
            [ws1, ws2],
            () => throw new InvalidOperationException("no WebKit"));

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        app.Window.NavigateTo("ws-2", null);
        await HeadlessExtensions.FlushAsync();

        // Switch back — ws-1's error must still be remembered.
        app.Window.NavigateTo("ws-1", null);
        await HeadlessExtensions.FlushAsync();
        Assert.That(app.Content.PortPaneShowsError, Is.True);
    }

    [AvaloniaTest]
    public async Task PortPane_hidesBrowserErrorBanner_whileFirstRunTutorialIsVisible()
    {
        var ws = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var tutorial = new FirstRunTutorialViewModel(
            new InMemoryTutorialSettingsStore(new FirstRunTutorialSettings(false, false, 0)),
            new PassingTutorialChecks());
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            ws,
            () => throw new InvalidOperationException("no WebKit installed"),
            tutorial);

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        Assert.That(app.Content.ShowsFirstRunTutorial, Is.True);
        Assert.That(app.Content.PortPaneShowsError, Is.False);
    }

    private sealed class InMemoryTutorialSettingsStore(FirstRunTutorialSettings settings) : IFirstRunTutorialSettingsStore
    {
        public Task<FirstRunTutorialSettings> LoadAsync() => Task.FromResult(settings);

        public Task SaveAsync(FirstRunTutorialSettings settings) => Task.CompletedTask;
    }

    private sealed class PassingTutorialChecks : IFirstRunTutorialChecks
    {
        public Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Docker works."));

        public Task<FirstRunCheckResult> CheckNodeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Node works."));

        public Task<FirstRunSampleProjectResult> CreateJavaScriptSampleAsync(string? currentProjectDirectory = null, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunSampleProjectResult.Success("Sample created.", currentProjectDirectory ?? "/tmp/tutorial/agent-up-tutorial/example-agent1"));

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

        public Task<FirstRunCheckResult> CreateDuplicatedJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Duplicate created."));

        public Task<FirstRunCheckResult> CheckDuplicatedJavaScriptWorkspacesAsync(string projectDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(FirstRunCheckResult.Success("Duplicate works."));
    }
}
