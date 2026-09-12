using AgentUp.Tests.Support;
using Avalonia.Threading;

namespace AgentUp.Tests.Features.Browser.E2E;

// End-to-end coverage of an OAuth 2.0 sign-in performed from inside the Agent-Up Desktop
// application, across both shapes real providers use:
//
//   * a same-tab redirect, which stays inside the workspace WebView, and
//   * a window.open() popup, which Desktop answers with a native web dialog — the behaviour
//     added for https://github.com/agent-up-oss/agent-up/issues/202 and the reason Google,
//     GitHub, Microsoft, and Auth0 sign-ins work at all inside a workspace.
//
// The flow is real: a loopback deployment plays the workspace application and its identity
// provider, and the authorization request, the redirect back with a one-time code, the PKCE
// verification, the back-channel token exchange, and the browser-held session cookie all happen
// over HTTP through the platform WebView (WebKitGTK on Linux, WKWebView on macOS, WebView2 on
// Windows).
//
// Run: dotnet test AgentUp.Tests/ --filter "Category=E2E"
[TestFixture, Category("E2E")]
public sealed class DesktopOAuthSignInFlowTests
{
    private const string StatusScript = "(function(){var e=document.getElementById('status');return e?e.textContent:'pending';})()";

    private OAuthTestServer _oauth = null!;
    private DesktopBrowserHarness _desktop = null!;

    [OneTimeSetUp]
    public async Task StartDesktop()
    {
        _oauth = new OAuthTestServer();
        _desktop = await DesktopBrowserHarness.LaunchAsync(_oauth.Port);
    }

    [OneTimeTearDown]
    public async Task StopDesktop()
    {
        await _desktop.DisposeAsync();
        _oauth.Dispose();
    }

    [SetUp]
    public async Task SignOut()
    {
        _oauth.Reset();
        await _desktop.NavigateAsync($"{_oauth.BaseUrl}?signout=1");
        await _desktop.WaitForScriptAsync(StatusScript, "signed-out", "The workspace page did not start signed out");
    }

    [Test, CancelAfter(60000), Timeout(60000)]
    public async Task OAuth_redirectSignIn_completesInsideTheWorkspaceWebView()
    {
        await _desktop.RunNavigatingScriptAsync("window.__signIn()");

        await _desktop.WaitForScriptAsync(
            StatusScript,
            $"signed-in:{OAuthTestServer.SignedInUser}",
            "The redirect sign-in never came back to the workspace page as an authenticated session");

        var authorize = await _oauth.WaitForAuthorizeRequestAsync();

        Assert.Multiple(() =>
        {
            Assert.That(authorize.GetValueOrDefault("response_type"), Is.EqualTo("code"));
            Assert.That(authorize.GetValueOrDefault("client_id"), Is.EqualTo(OAuthTestServer.ClientId));
            Assert.That(authorize.GetValueOrDefault("code_challenge_method"), Is.EqualTo("S256"));
            Assert.That(authorize.GetValueOrDefault("state", string.Empty), Is.Not.Empty, "The client must carry CSRF state through the redirect");
            Assert.That(_oauth.TokenGrants, Is.EqualTo(1), "The authorization code must be exchanged exactly once");
            Assert.That(_oauth.RejectedRequests, Is.Zero, "No step of the flow may be rejected");
        });
    }

    [Test, CancelAfter(60000), Timeout(60000)]
    public async Task OAuth_popupSignIn_authorizesInANativePopupAndSignsInTheWorkspacePage()
    {
        var popupsBefore = _desktop.Window.OpenPopupCountForTests;
        var handled = await Dispatcher.UIThread.InvokeAsync(() =>
            NativeWebViewEvents.RaiseNewWindowRequested(_desktop.WorkspaceWebView, new Uri($"{_oauth.BaseUrl}signin")));

        Assert.Multiple(() =>
        {
            Assert.That(handled.Handled, Is.True, "Desktop must claim the request so the engine does not drop the popup");
            Assert.That(_desktop.Window.OpenPopupCountForTests, Is.EqualTo(popupsBefore + 1),
                "Desktop must open exactly one native sign-in popup for the authorization request");
        });

        var authorize = await _oauth.WaitForAuthorizeRequestAsync();

        // The popup is a separate native window, but it shares the engine's cookie store with
        // the workspace WebView — which is what lets the sign-in it completes take effect in the
        // application the user was already looking at.
        await _desktop.WaitForScriptAsync(
            StatusScript,
            $"signed-in:{OAuthTestServer.SignedInUser}",
            "The sign-in completed in the popup never reached the workspace page");

        Assert.Multiple(() =>
        {
            Assert.That(authorize.GetValueOrDefault("code_challenge_method"), Is.EqualTo("S256"));
            Assert.That(_oauth.TokenGrants, Is.EqualTo(1), "The popup's authorization code must be exchanged exactly once");
            Assert.That(_oauth.RejectedRequests, Is.Zero, "No step of the popup flow may be rejected");
        });
    }

    [Test, CancelAfter(60000), Timeout(60000)]
    public async Task OAuth_popupForNonHttpScheme_isRefusedWithoutOpeningAWindow()
    {
        var popupsBefore = _desktop.Window.OpenPopupCountForTests;
        var handled = await Dispatcher.UIThread.InvokeAsync(() =>
            NativeWebViewEvents.RaiseNewWindowRequested(_desktop.WorkspaceWebView, new Uri("about:blank")));

        Assert.Multiple(() =>
        {
            Assert.That(handled.Handled, Is.True);
            Assert.That(_desktop.Window.OpenPopupCountForTests, Is.EqualTo(popupsBefore),
                "Only http and https sign-in destinations may open a Desktop popup");
        });
    }
}
