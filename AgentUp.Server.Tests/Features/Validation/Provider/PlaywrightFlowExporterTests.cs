using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Providers;
namespace AgentUp.Server.Tests.Features.Validation.Provider;
public sealed class PlaywrightFlowExporterTests
{
    [Test]
    public void Export_uses_relative_base_semantic_locators_and_outcome_assertions()
    {
        var flow = Flow("Customer completes checkout",
            [new(ValidationExpectation.Text, "Your cart")],
            [new("checkout", "Continue to checkout", ValidationAction.Click, new(Role: "button", Name: "Checkout", Selector: "#checkout"), Expectations: [new(ValidationExpectation.Text, "Order confirmed")])]);
        var result = new PlaywrightFlowExporter().Export(flow);
        Assert.Multiple(() => { Assert.That(result.FileName, Is.EqualTo("customer-completes-checkout.spec.ts")); Assert.That(result.Content, Does.Contain("process.env.AGENT_UP_BASE_URL")); Assert.That(result.Content, Does.Contain("getByRole(\"button\", { name: \"Checkout\" })")); Assert.That(result.Content, Does.Contain("getByText(\"Order confirmed\"")); Assert.That(result.Content, Does.Not.Contain("waitForTimeout")); });
    }

    [Test]
    public void Export_writesNavigateFillAndPressSteps()
    {
        var flow = Flow("Sign in",
            [new(ValidationExpectation.Text, "Sign in")],
            [
                new("go", "Open the sign-in page", ValidationAction.Navigate, null, "/login", [new(ValidationExpectation.Text, "Email")]),
                new("email", "Type the email address", ValidationAction.Fill, new(Selector: "#email"), "a@b.test", [new(ValidationExpectation.Text, "Email")]),
                new("submit", "Press enter to submit", ValidationAction.Press, null, "Enter", [new(ValidationExpectation.Text, "Welcome")])
            ]);

        var content = new PlaywrightFlowExporter().Export(flow).Content;

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("page.goto(new URL(\"/login\""));
            Assert.That(content, Does.Contain("page.locator(\"#email\").fill(\"a@b.test\")"));
            Assert.That(content, Does.Contain("page.keyboard.press(\"Enter\")"));
            Assert.That(content, Does.Contain("await test.step(\"Open the sign-in page\""));
        });
    }

    [Test]
    public void Export_resolvesARelativeUrlAssertionAgainstTheBaseUrl_andAnAbsoluteOneDirectly()
    {
        var relative = new PlaywrightFlowExporter().Export(
            Flow("Relative", [new(ValidationExpectation.Url, "/cart")], [Step()])).Content;
        var absolute = new PlaywrightFlowExporter().Export(
            Flow("Absolute", [new(ValidationExpectation.Url, "https://shop.test/cart")], [Step()])).Content;

        Assert.Multiple(() =>
        {
            Assert.That(relative, Does.Contain("toHaveURL(new URL(\"/cart\", process.env.AGENT_UP_BASE_URL!).toString())"));
            Assert.That(absolute, Does.Contain("toHaveURL(\"https://shop.test/cart\")"));
        });
    }

    [Test]
    public void Export_writesTitleAndVisibilityAssertions()
    {
        var flow = Flow("Titles",
            [
                new(ValidationExpectation.Title, "Shop"),
                new(ValidationExpectation.Visible, "ignored", new ValidationTarget(TestId: "cart"))
            ],
            [Step()]);

        var content = new PlaywrightFlowExporter().Export(flow).Content;

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("toHaveTitle(\"Shop\")"));
            Assert.That(content, Does.Contain("expect(page.getByTestId(\"cart\")).toBeVisible()"));
        });
    }

    [Test]
    public void Export_rejectsAVisibilityAssertionWithNoTarget()
    {
        var flow = Flow("No target", [new(ValidationExpectation.Visible, "ignored")], [Step()]);

        Assert.That(() => new PlaywrightFlowExporter().Export(flow),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("requires a target"));
    }

    // Save-time validation demands a selector for Click and Fill, so a target-less interactive step
    // means a hand-edited validation-flows.json. It must not silently emit "await .click();".
    [TestCase(ValidationAction.Click)]
    [TestCase(ValidationAction.Fill)]
    public void Export_rejectsAnInteractiveStepWithNoTarget(ValidationAction action)
    {
        var flow = Flow("Target-less", [new(ValidationExpectation.Text, "Cart")],
            [new("orphan", "Click nothing", action, null, "x", [new(ValidationExpectation.Text, "Cart")])]);

        Assert.That(() => new PlaywrightFlowExporter().Export(flow),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("orphan"));
    }

    [Test]
    public void Export_rejectsATargetThatCarriesNoUsableLocator()
    {
        var flow = Flow("Empty target", [new(ValidationExpectation.Text, "Cart")],
            [new("blank", "Click a blank target", ValidationAction.Click, new ValidationTarget(), null, [new(ValidationExpectation.Text, "Cart")])]);

        Assert.That(() => new PlaywrightFlowExporter().Export(flow),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("semantic target or selector"));
    }

    [TestCaseSource(nameof(LocatorCases))]
    public void Export_prefersTheMostSemanticLocatorAvailable(ValidationTarget target, string expected)
    {
        var flow = Flow("Locators", [new(ValidationExpectation.Text, "Cart")],
            [new("act", "Click the control", ValidationAction.Click, target, null, [new(ValidationExpectation.Text, "Cart")])]);

        Assert.That(new PlaywrightFlowExporter().Export(flow).Content, Does.Contain(expected));
    }

    private static IEnumerable<TestCaseData> LocatorCases()
    {
        yield return new TestCaseData(new ValidationTarget(Label: "Email"), "page.getByLabel(\"Email\")").SetName("label");
        yield return new TestCaseData(new ValidationTarget(TestId: "cart"), "page.getByTestId(\"cart\")").SetName("testid");
        yield return new TestCaseData(new ValidationTarget(Text: "Buy"), "page.getByText(\"Buy\", { exact: true })").SetName("text");
        yield return new TestCaseData(new ValidationTarget(Selector: ".buy"), "page.locator(\".buy\")").SetName("selector");
        yield return new TestCaseData(new ValidationTarget(Role: "link", Text: "Home"), "page.getByRole(\"link\", { name: \"Home\" })").SetName("role falls back to text for the name");
    }

    [Test]
    public void Export_slugsTheFlowNameIntoAFileName()
    {
        var content = new PlaywrightFlowExporter().Export(
            Flow("  Checkout: works (twice!)  ", [new(ValidationExpectation.Text, "Cart")], [Step()]));

        Assert.That(content.FileName, Is.EqualTo("checkout-works-twice.spec.ts"));
    }

    private static ValidationFlow Flow(
        string name,
        IReadOnlyList<ValidationAssertion> initialExpectations,
        IReadOnlyList<ValidationStep> steps) =>
        new("flow", "ws", "shop", name, "A shopper can finish checkout.", "/cart",
            initialExpectations, steps, DateTimeOffset.UtcNow);

    private static ValidationStep Step() =>
        new("checkout", "Continue to checkout", ValidationAction.Click,
            new ValidationTarget(Selector: "#checkout"), null, [new ValidationAssertion(ValidationExpectation.Text, "Done")]);
}
