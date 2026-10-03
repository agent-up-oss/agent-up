using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Applications.Headless;

[TestFixture]
public sealed class RemoteApplicationTabTests
{
    [AvaloniaTest]
    public async Task RemoteServer_OpensHttpApplicationThroughTicketedProxy()
    {
        var workspace = DesktopDomain.WorkspaceServing(4312).Build();
        NativeWebView? webView = null;
        var (app, handler) = await AppDriver.LaunchWithFakeHttpAsync(
            workspace,
            () => webView = new NativeWebView(),
            new Uri("https://remote.example/"),
            configured => configured.ApplicationProxyTicket = () => FakeHttpMessageHandler.JsonOk(new
            {
                ticket = "desktop-ticket",
                bootstrapPath = $"/apps/{workspace.Id}/4312",
                expiresAt = DateTimeOffset.UtcNow.AddSeconds(30)
            }));

        app.Window.NavigateTo(workspace.Id, "http://127.0.0.1:4312/docs?view=full");
        await WaitUntilAsync(() =>
            handler.ApplicationProxyTicketRequests >= 2
            && webView?.Source?.Fragment.Contains("return=", StringComparison.Ordinal) == true);

        Assert.Multiple(() =>
        {
            Assert.That(webView!.Source!.Host, Is.EqualTo("remote.example"));
            Assert.That(webView.Source.AbsolutePath, Is.EqualTo($"/apps/{workspace.Id}/4312"));
            Assert.That(
                webView.Source.Fragment,
                Is.EqualTo("#ticket=desktop-ticket&return=%2Fdocs%3Fview%3Dfull"));
        });
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (condition())
                return;

            await HeadlessExtensions.FlushAsync();
            await Task.Delay(50);
        }

        Assert.Fail("Timed out waiting for remote application proxy navigation.");
    }
}
