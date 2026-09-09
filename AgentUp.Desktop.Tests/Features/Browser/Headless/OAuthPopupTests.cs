using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Browser.Headless;

// Regression coverage for https://github.com/agent-up-oss/agent-up/issues/202 —
// OAuth/sign-in flows inside the embedded workspace browser rely on window.open() popups
// (Google, GitHub, Microsoft, Auth0, ...). Those popups were silently dropped because
// NativeWebView.NewWindowRequested was never handled, so window.open() had nowhere to go.
[TestFixture]
public sealed class OAuthPopupTests
{
    [AvaloniaTest]
    public async Task NewWindowRequested_forOAuthPopup_opensAndNavigatesAPopup()
    {
        NativeWebView? webView = null;
        var popup = new FakeWebPopup();
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            WorkspaceFixtures.WithHttpPort("ws-1", 3000),
            () => webView = new NativeWebView());
        app.Window.WebPopupFactory = () => popup;

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();
        Assert.That(webView, Is.Not.Null);

        RaiseNewWindowRequested(webView!, new Uri("https://accounts.google.com/o/oauth2/auth?client_id=demo"));
        await HeadlessExtensions.FlushAsync();

        Assert.That(popup.NavigatedTo, Is.EqualTo(new Uri("https://accounts.google.com/o/oauth2/auth?client_id=demo")));
        Assert.That(popup.ShowCalled, Is.True);
        Assert.That(popup.DisposeCalled, Is.False);
        Assert.That(app.Window.OpenPopupCountForTests, Is.EqualTo(1));
    }

    [AvaloniaTest]
    public async Task NewWindowRequested_marksEventHandled_soBrowserDoesNotAlsoOpenItsOwnWindow()
    {
        NativeWebView? webView = null;
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            WorkspaceFixtures.WithHttpPort("ws-1", 3000),
            () => webView = new NativeWebView());
        app.Window.WebPopupFactory = () => new FakeWebPopup();

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        var args = new WebViewNewWindowRequestedEventArgs { Request = new Uri("https://github.com/login/oauth/authorize") };
        RaiseNewWindowRequested(webView!, args);
        await HeadlessExtensions.FlushAsync();

        Assert.That(args.Handled, Is.True);
    }

    [AvaloniaTest]
    public async Task NewWindowRequested_whenPopupCreationFails_doesNotCrashAndLeavesNoOpenPopup()
    {
        NativeWebView? webView = null;
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            WorkspaceFixtures.WithHttpPort("ws-1", 3000),
            () => webView = new NativeWebView());
        app.Window.WebPopupFactory = () => throw new InvalidOperationException("no WebKit installed");

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        // If HandleNewWindowRequested lets the exception escape, this call itself throws and
        // the test fails with it — no explicit Assert.DoesNotThrow wrapper needed (and using
        // one here deadlocks: it blocks the Avalonia dispatcher thread synchronously while the
        // awaited FlushAsync below needs that same thread pumped).
        RaiseNewWindowRequested(webView!, new Uri("https://accounts.google.com/o/oauth2/auth"));
        await HeadlessExtensions.FlushAsync();

        Assert.That(app.Window.OpenPopupCountForTests, Is.EqualTo(0));
    }

    [AvaloniaTest]
    public async Task NewWindowRequested_forNonHttpScheme_isIgnored()
    {
        NativeWebView? webView = null;
        var factoryCalls = 0;
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            WorkspaceFixtures.WithHttpPort("ws-1", 3000),
            () => webView = new NativeWebView());
        app.Window.WebPopupFactory = () =>
        {
            factoryCalls++;
            return new FakeWebPopup();
        };

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        RaiseNewWindowRequested(webView!, new Uri("about:blank"));
        await HeadlessExtensions.FlushAsync();

        Assert.That(factoryCalls, Is.EqualTo(0));
        Assert.That(app.Window.OpenPopupCountForTests, Is.EqualTo(0));
    }

    [AvaloniaTest]
    public async Task ClosingPopup_removesItFromTracking()
    {
        NativeWebView? webView = null;
        var popup = new FakeWebPopup();
        var app = await AppDriver.LaunchWithWorkspaceAsync(
            WorkspaceFixtures.WithHttpPort("ws-1", 3000),
            () => webView = new NativeWebView());
        app.Window.WebPopupFactory = () => popup;

        app.Window.NavigateTo("ws-1", "http://localhost:3000/");
        await HeadlessExtensions.FlushAsync();

        RaiseNewWindowRequested(webView!, new Uri("https://accounts.google.com/o/oauth2/auth"));
        await HeadlessExtensions.FlushAsync();
        Assert.That(app.Window.OpenPopupCountForTests, Is.EqualTo(1));

        popup.RaiseClosing();
        await HeadlessExtensions.FlushAsync();

        Assert.That(app.Window.OpenPopupCountForTests, Is.EqualTo(0));
        Assert.That(popup.DisposeCalled, Is.True);
    }

    // NativeWebView.NewWindowRequested has no public way to raise it externally (it's a genuine
    // native-engine callback), so tests trigger it the same way the underlying WebKit/WebView2
    // engine would — by invoking the event's backing delegate directly.
    private static void RaiseNewWindowRequested(NativeWebView webView, Uri requestedUri)
        => RaiseNewWindowRequested(webView, new WebViewNewWindowRequestedEventArgs { Request = requestedUri });

    private static void RaiseNewWindowRequested(NativeWebView webView, WebViewNewWindowRequestedEventArgs args)
    {
        var field = typeof(NativeWebView).GetField("_newWindowRequested", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("NativeWebView._newWindowRequested field not found — package layout changed.");
        var handler = (EventHandler<WebViewNewWindowRequestedEventArgs>?)field.GetValue(webView);
        handler?.Invoke(webView, args);
    }

    private sealed class FakeWebPopup : IWebPopup
    {
        public string? TitleSet { get; private set; }
        public Uri? NavigatedTo { get; private set; }
        public bool ShowCalled { get; private set; }
        public bool DisposeCalled { get; private set; }

        public string Title { set => TitleSet = value; }

        public event EventHandler? Closing;

        public void Navigate(Uri uri) => NavigatedTo = uri;

        public void Show() => ShowCalled = true;

        public void Dispose() => DisposeCalled = true;

        public void RaiseClosing() => Closing?.Invoke(this, EventArgs.Empty);
    }
}
