using System.Net;
using System.Net.Http;
using System.Text;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Applications.Headless;

[TestFixture]
public sealed class DesktopApplicationTabTests
{
    [AvaloniaTest]
    public async Task SubNavBar_showsDesktopLabel_insteadOfTypeName()
    {
        var workspace = WorkspaceFixtures.WithHttpAndDesktop();
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace, () => new NativeWebView());

        await app.Content.SelectApplicationTabAsync();
        await app.Content.SelectApplicationByIndexAsync(1);
        await WaitUntilAsync(() => app.Content.SubNavBarLabels.Contains("Desktop"));

        Assert.Multiple(() =>
        {
            Assert.That(app.Content.SubNavBarLabels, Does.Contain("Desktop"));
            Assert.That(
                app.Content.SubNavBarLabels,
                Does.Not.Contain("AgentUp.Desktop.Features.Applications.ViewModels.DesktopSubTabViewModel"));
        });
    }

    [AvaloniaTest]
    public async Task SwitchingToDesktop_hidesHttpWebView_andShowsConnecting()
    {
        var workspace = WorkspaceFixtures.WithHttpAndDesktop();
        var (app, handler) = await AppDriver.LaunchWithFakeHttpAsync(workspace, () => new NativeWebView());
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.ViewerTicket = async _ =>
        {
            await gate.Task;
            return FakeHttpMessageHandler.JsonOk(ViewerTicket());
        };

        await app.Content.SelectApplicationTabAsync();
        app.Window.NavigateTo(workspace.Id, "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();
        Assert.That(app.Window.ArePortWebViewsHiddenForTests, Is.False);

        await app.Content.SelectApplicationByIndexAsync(1);
        await WaitUntilAsync(() => app.Content.ShowsDesktopConnecting);

        Assert.Multiple(() =>
        {
            Assert.That(app.Content.ShowsDesktopConnecting, Is.True);
            Assert.That(app.Window.IsDesktopConnectingForTests, Is.True);
            Assert.That(app.Window.ArePortWebViewsHiddenForTests, Is.True);
            Assert.That(app.Content.PortPaneShowsError, Is.False);
            Assert.That(app.Content.SubNavBarLabels, Does.Contain("Desktop"));
        });

        gate.SetResult(true);
        await WaitUntilAsync(() => app.Window.IsDesktopWebViewVisibleForTests);

        Assert.Multiple(() =>
        {
            Assert.That(app.Content.ShowsDesktopConnecting, Is.False);
            Assert.That(app.Window.IsDesktopWebViewVisibleForTests, Is.True);
        });
    }

    [AvaloniaTest]
    public async Task DesktopViewerTicket_retriesUntilTheServerReturnsAViewer()
    {
        var workspace = WorkspaceFixtures.WithHttpAndDesktop();
        var (app, handler) = await AppDriver.LaunchWithFakeHttpAsync(workspace, () => new NativeWebView());
        handler.ViewerTicket = attempt => Task.FromResult(
            attempt < 3
                ? FakeHttpMessageHandler.NotFound()
                : FakeHttpMessageHandler.JsonOk(ViewerTicket()));

        await app.Content.SelectApplicationTabAsync();
        await app.Content.SelectApplicationByIndexAsync(1);
        await WaitUntilAsync(() => app.Window.IsDesktopWebViewVisibleForTests);

        Assert.Multiple(() =>
        {
            Assert.That(app.Window.IsDesktopWebViewVisibleForTests, Is.True);
            Assert.That(app.Content.ShowsDesktopConnecting, Is.False);
            Assert.That(app.Content.PortPaneShowsError, Is.False);
        });
    }

    [AvaloniaTest]
    public async Task DesktopViewerTicket_showsError_whenTheApplicationHasFailed()
    {
        var workspace = WorkspaceFixtures.WithHttpAndDesktop(desktopState: "Failed");
        var app = await AppDriver.LaunchWithWorkspaceAsync(workspace, () => new NativeWebView());

        await app.Content.SelectApplicationTabAsync();
        app.Window.NavigateTo(workspace.Id, "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        await app.Content.SelectApplicationByIndexAsync(1);
        await WaitUntilAsync(() => app.Content.PortPaneShowsError);

        Assert.Multiple(() =>
        {
            Assert.That(app.Content.ShowsDesktopConnecting, Is.False);
            Assert.That(app.Window.ArePortWebViewsHiddenForTests, Is.True);
            Assert.That(app.Content.PortPaneShowsError, Is.True);
            Assert.That(app.Content.WebViewErrorMessage, Does.Contain("Could not open the desktop application"));
        });
    }

    [AvaloniaTest]
    public async Task DesktopViewerTicket_retriesHttpFailuresUntilTheServerReturnsAViewer()
    {
        var workspace = WorkspaceFixtures.WithHttpAndDesktop();
        var (app, handler) = await AppDriver.LaunchWithFakeHttpAsync(workspace, () => new NativeWebView());
        handler.ViewerTicket = attempt =>
        {
            if (attempt < 3)
                throw new HttpRequestException("desktop ticket endpoint is not ready");
            return Task.FromResult(FakeHttpMessageHandler.JsonOk(ViewerTicket()));
        };

        await app.Content.SelectApplicationTabAsync();
        await app.Content.SelectApplicationByIndexAsync(1);
        await WaitUntilAsync(() => app.Window.IsDesktopWebViewVisibleForTests);

        Assert.That(app.Window.IsDesktopWebViewVisibleForTests, Is.True);
    }

    [AvaloniaTest]
    public async Task DesktopViewerTicket_showsError_whenTheServerReturnsAnInvalidViewer()
    {
        var workspace = WorkspaceFixtures.WithHttpAndDesktop();
        var (app, handler) = await AppDriver.LaunchWithFakeHttpAsync(workspace, () => new NativeWebView());
        handler.ViewerTicket = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        });

        await app.Content.SelectApplicationTabAsync();
        await app.Content.SelectApplicationByIndexAsync(1);
        await WaitUntilAsync(() => app.Content.PortPaneShowsError);

        Assert.That(app.Content.WebViewErrorMessage, Does.Contain("Could not open the desktop application"));
    }

    [AvaloniaTest]
    public async Task SwitchingAwayFromDesktop_cancelsAnInFlightTicketWait()
    {
        var workspace = WorkspaceFixtures.WithHttpAndDesktop();
        var (app, handler) = await AppDriver.LaunchWithFakeHttpAsync(workspace, () => new NativeWebView());
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.ViewerTicket = async _ =>
        {
            await gate.Task;
            return FakeHttpMessageHandler.JsonOk(ViewerTicket());
        };

        await app.Content.SelectApplicationTabAsync();
        await app.Content.SelectApplicationByIndexAsync(1);
        await WaitUntilAsync(() => app.Content.ShowsDesktopConnecting);

        await app.Content.SelectApplicationByIndexAsync(0);
        gate.SetResult(true);
        await HeadlessExtensions.FlushAsync();
        await Task.Delay(50);

        Assert.That(app.Window.IsDesktopWebViewVisibleForTests, Is.False);
    }

    private static DesktopViewerTicketResponse ViewerTicket() =>
        new("/api/desktop-applications/session/s1/viewer?ticket=test", DateTimeOffset.UtcNow.AddMinutes(5));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (condition())
                return;

            await HeadlessExtensions.FlushAsync();
            await Task.Delay(50);
        }

        Assert.Fail("Timed out waiting for the desktop application tab to update.");
    }
}
