using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Browser.Headless;

[TestFixture]
public sealed class WebViewLifecycleTests
{
    [AvaloniaTest]
    public async Task ClosedWindow_ignoresLateBrowserNavigation()
    {
        var workspace = WorkspaceFixtures.WithHttpPort("ws-1", 3000);
        var createdWebViews = 0;
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            workspace,
            () =>
            {
                createdWebViews++;
                throw new InvalidOperationException("WebView should not be created after close.");
            });

        app.Window.Close();
        await HeadlessExtensions.FlushAsync();
        createdWebViews = 0;

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        Assert.That(createdWebViews, Is.EqualTo(0));
    }
}
