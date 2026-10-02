using AgentUp.AUDebug.Features.Screens.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

[TestFixture]
public sealed class MobileScreenScriptProviderTests
{
    [Test]
    public void Navigate_routesInsideTheAppRatherThanReloadingIt()
    {
        var script = MobileScreenScriptProvider.Navigate("/workspace/harbor-shop/git/review");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("pushState"));
            Assert.That(script, Does.Contain("popstate"));
            Assert.That(script, Does.Not.Contain("location.href ="));
        });
    }

    [Test]
    public void Locate_prefersAnExactLabelOverAPrefix()
    {
        var script = MobileScreenScriptProvider.Locate("Commit");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("read(node) === want"));
            Assert.That(script, Does.Contain("startsWith(want)"));
            Assert.That(script, Does.Contain("exact.length > 0"));
        });
    }

    [Test]
    public void Locate_onlyReturnsAPointThatHitTestsToTheElement()
    {
        var script = MobileScreenScriptProvider.Locate("Close sidebar");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("elementFromPoint"));
            Assert.That(script, Does.Contain("target.contains(hit)"));
        });
    }

    [Test]
    public void FocusField_matchesPlaceholderOrAccessibleLabel()
    {
        var script = MobileScreenScriptProvider.FocusField("Server URL");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("placeholder"));
            Assert.That(script, Does.Contain("aria-label"));
            Assert.That(script, Does.Contain("\"Server URL\""));
        });
    }
}
