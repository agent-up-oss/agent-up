using AgentUp.Desktop.Features.Browser.Controllers;

namespace AgentUp.Desktop.Tests.Features.Browser.Controller;

[TestFixture]
public sealed class BrowserControllersTests
{
    [Test]
    public void Interaction_scripts_escape_user_supplied_selectors_and_text()
    {
        var controller = new BrowserInteractionController();

        Assert.Multiple(() =>
        {
            Assert.That(controller.FillScript("#name'field", "O'Reilly"), Does.Contain("O\\u0027Reilly"));
            Assert.That(controller.CheckSelectorScript("[data-name='x']"), Does.Contain("data-name"));
            Assert.That(controller.CheckTextScript("ready"), Does.Contain("ready"));
        });
    }

    [Test]
    public void Interaction_controller_exposes_navigation_and_page_metadata_scripts()
    {
        var controller = new BrowserInteractionController();

        Assert.Multiple(() =>
        {
            Assert.That(controller.AnimationMs, Is.GreaterThan(0));
            Assert.That(controller.CheckNavigationScript(), Does.Contain("document.readyState"));
            Assert.That(controller.GetUrlScript(), Does.Contain("location"));
            Assert.That(controller.GetTitleScript(), Does.Contain("title"));
        });
    }

    [Test]
    public async Task Viewport_controller_forwards_navigation_and_evaluation()
    {
        (string Workspace, string? Url) navigation = default;
        var controller = new BrowserViewportController(
            (workspace, url) => navigation = (workspace, url),
            (workspace, script) => Task.FromResult<string?>($"{workspace}:{script}"));

        controller.NavigateTo("one", "https://example.test");
        var result = await controller.EvalAsync("one", "document.title");

        Assert.That(navigation, Is.EqualTo(("one", "https://example.test")));
        Assert.That(result, Is.EqualTo("one:document.title"));
    }
}
