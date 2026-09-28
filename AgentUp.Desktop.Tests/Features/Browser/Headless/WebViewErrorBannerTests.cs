using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Tests.Support;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Tests.Features.Browser.Headless;

[TestFixture]
public sealed class WebViewErrorBannerTests
{
    [Test]
    public void BrowserErrorPage_usesBrandedDarkTheme()
    {
        var html = MainWindow.BuildBrowserErrorHtml(
            "Not found 404",
            "Not Found",
            new Uri("http://localhost:3000/missing"));

        Assert.That(html, Does.Contain($"background: {AgentUpThemeColors.Canvas}"));
        Assert.That(html, Does.Contain($"border: 1px solid {AgentUpThemeColors.BorderSubtle}"));
        Assert.That(html, Does.Contain($"color: {AgentUpThemeColors.AccentSoft}"));
        Assert.That(html, Does.Contain("Not found 404"));
        Assert.That(html, Does.Contain("http://localhost:3000/missing"));
        Assert.That(html, Does.Not.Contain("Agent-Up browser"));
        Assert.That(html, Does.Not.Contain("Not Found</p>"));
    }

    [Test]
    public void BrowserErrorPage_escapesServerContent()
    {
        var html = MainWindow.BuildBrowserErrorHtml(
            "<Not Found>",
            "route has <script>alert('x')</script>",
            new Uri("http://localhost:3000/a?b=<c>"));

        Assert.That(html, Does.Contain("&lt;Not Found&gt;"));
        Assert.That(html, Does.Contain("&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;"));
        Assert.That(html, Does.Contain("http://localhost:3000/a?b=&lt;c&gt;"));
        Assert.That(html, Does.Not.Contain("<script>alert"));
    }

    [AvaloniaTest]
    public async Task PortPane_showsErrorBanner_whenWebViewCreationFails()
    {
        var ws = DesktopDomain.WorkspaceServing(3000).Build();
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
        var ws1 = DesktopDomain.WorkspaceServing(3000).Build();
        var ws2 = DesktopDomain.WorkspaceServing(4000, "ws-2").Build();
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
        var ws1 = DesktopDomain.WorkspaceServing(3000).Build();
        var ws2 = DesktopDomain.WorkspaceServing(4000, "ws-2").Build();
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
    public async Task MainWindow_close_clearsBrowserStateAfterWebViewFailure()
    {
        var ws = DesktopDomain.WorkspaceServing(3000).Build();
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            ws,
            () => throw new InvalidOperationException("no WebKit installed"));

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();
        Assert.That(app.Window.HasBrowserResourcesForTests, Is.True);

        app.Window.Close();
        await HeadlessExtensions.FlushAsync();

        Assert.That(app.Window.HasBrowserResourcesForTests, Is.False);
    }
}
