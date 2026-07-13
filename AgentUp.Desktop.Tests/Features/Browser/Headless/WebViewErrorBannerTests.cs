using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Browser.Headless;

[TestFixture]
public sealed class WebViewErrorBannerTests
{
    [AvaloniaTest]
    public async Task PortPane_showsErrorBanner_whenWebViewCreationFails()
    {
        var ws = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var app = await AppDriver.LaunchWithWorkspaceAsync(ws);
        app.Window.WebViewFactory = () => throw new InvalidOperationException("no WebKit installed");

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
        var app = await AppDriver.LaunchWithWorkspacesAsync([ws1, ws2]);
        app.Window.WebViewFactory = () => throw new InvalidOperationException("no WebKit");

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
        var app = await AppDriver.LaunchWithWorkspacesAsync([ws1, ws2]);
        app.Window.WebViewFactory = () => throw new InvalidOperationException("no WebKit");

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        app.Window.NavigateTo("ws-2", null);
        await HeadlessExtensions.FlushAsync();

        // Switch back — ws-1's error must still be remembered.
        app.Window.NavigateTo("ws-1", null);
        await HeadlessExtensions.FlushAsync();
        Assert.That(app.Content.PortPaneShowsError, Is.True);
    }
}
