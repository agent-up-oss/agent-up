using System.Net.Http;
using System.Reactive.Linq;
using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Authentication.Controllers;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Features.Authentication.Services;
using AgentUp.Desktop.Features.Authentication.ViewModels;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Providers;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Browser.Headless;

[TestFixture]
public sealed class DemoApplicationWebViewTests
{
    [AvaloniaTest]
    public async Task DemoHttpTab_loadsTheBundledPageThroughTheFakeBackend()
    {
        var probed = false;
        var paths = new List<string>();
        NativeWebView? webView = null;
        var window = await LaunchDemoWindowAsync(
            () => webView = new NativeWebView(),
            uri =>
            {
                probed = true;
                return Task.FromResult<string?>(MainWindow.BuildBrowserErrorHtml(
                    "probed",
                    "Demo should not probe the allocated port.",
                    uri));
            },
            paths);

        window.NavigateTo("harbor-shop", "http://localhost:9100/");
        await WaitUntilAsync(() => webView?.Source is { IsFile: true });

        Assert.Multiple(() =>
        {
            Assert.That(probed, Is.False);
            Assert.That(paths, Does.Contain("/api/apps/tickets"));
            Assert.That(paths, Does.Contain("/apps/harbor-shop/storefront"));
            Assert.That(File.ReadAllText(webView!.Source!.LocalPath), Does.Contain("Harbor Shop"));
            Assert.That(File.ReadAllText(webView.Source.LocalPath), Does.Contain("Place order"));
        });
    }

    [AvaloniaTest]
    public async Task DemoHttpTab_showsAMissingPageWhenThePortHasNoDemoHtml()
    {
        NativeWebView? webView = null;
        var window = await LaunchDemoWindowAsync(() => webView = new NativeWebView());

        window.NavigateTo("harbor-shop", "http://localhost:1/");
        await WaitUntilAsync(() => webView?.Source is { IsFile: true });

        Assert.That(
            File.ReadAllText(webView!.Source!.LocalPath),
            Does.Contain("The demo application page is missing."));
    }

    private static async Task<MainWindow> LaunchDemoWindowAsync(
        Func<NativeWebView> webViewFactory,
        Func<Uri, Task<string?>>? browserProbe = null,
        List<string>? requestPaths = null)
    {
        var backend = FakeServerTestComposition.Backend();
        var fakeServers = FakeServerTestComposition.Controller(backend);
        var inner = new FakeServerMessageHandler(backend, new HttpClientHandler());
        var http = ServerSessionProvider.CreateClient(
            new Uri("http://127.0.0.1:5000"),
            requestPaths is null ? inner : new RecordingHandler(requestPaths, inner));
        var connections = FakeServerTestComposition.Connections(
            new InMemoryServerConnectionStore(),
            http,
            backend);
        connections.Activate("fake");
        var login = new LoginViewModel(new AuthenticationController(
            new AuthenticationService(new AuthenticationApiClient(http)),
            connections));
        login.ShowPicker();
        login.ServerUrl = FakeServerIdentity.Url;
        await login.ConnectCommand.Execute().FirstAsync();

        var window = new MainWindow(http, fakeServers)
        {
            DataContext = MainViewModelFactory.Create(http, login)
        };
        window.WebViewFactory = webViewFactory;
        if (browserProbe is not null)
            window.BrowserProbe = browserProbe;
        window.Show();
        return window;
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

        Assert.Fail("Timed out waiting for the demo application WebView to load.");
    }

    private sealed class RecordingHandler(List<string> paths, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
                paths.Add(request.RequestUri.AbsolutePath);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
