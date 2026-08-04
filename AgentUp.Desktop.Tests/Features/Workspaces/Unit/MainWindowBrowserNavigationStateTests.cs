using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class MainWindowBrowserNavigationStateTests
{
    [Test]
    public void HeadlessBrowser_keeps_page_state_when_returning_to_current_url()
    {
        var shouldNavigate = MainWindow.ShouldPostHeadlessNavigate(
            "http://localhost:3000/dashboard",
            "http://localhost:3000/dashboard");

        Assert.That(shouldNavigate, Is.False);
    }

    [Test]
    public void HeadlessBrowser_navigates_when_application_url_changes()
    {
        var shouldNavigate = MainWindow.ShouldPostHeadlessNavigate(
            "http://localhost:3000/dashboard",
            "http://localhost:5000/swagger");

        Assert.That(shouldNavigate, Is.True);
    }
}
