using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserNavigationStateTests
{
    [Test]
    public void ExistingWebView_navigates_when_requested_url_differs_from_last_known_url()
    {
        var shouldNavigate = MainWindow.ShouldNavigateExistingWebView(
            "http://localhost:3000/pre-warm/ws-1",
            "http://localhost:3000/set/logged_in/true");

        Assert.That(shouldNavigate, Is.True);
    }

    [Test]
    public void ExistingWebView_keeps_page_state_when_requested_url_matches_last_known_url()
    {
        var shouldNavigate = MainWindow.ShouldNavigateExistingWebView(
            "http://localhost:3000/dashboard",
            "http://localhost:3000/dashboard");

        Assert.That(shouldNavigate, Is.False);
    }

    [Test]
    public void ExistingWebView_recovers_error_state_when_last_known_url_is_missing()
    {
        var shouldNavigate = MainWindow.ShouldNavigateExistingWebView(
            null,
            "http://localhost:3000/dashboard");

        Assert.That(shouldNavigate, Is.True);
    }
}
