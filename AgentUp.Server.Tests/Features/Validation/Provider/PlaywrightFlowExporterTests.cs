using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Providers;
namespace AgentUp.Server.Tests.Features.Validation.Provider;
public sealed class PlaywrightFlowExporterTests
{
    [Test]
    public void Export_uses_relative_base_semantic_locators_and_outcome_assertions()
    {
        var flow = new ValidationFlow("flow", "ws", "shop", "Customer completes checkout", "A shopper can finish checkout.", "/cart",
            [new(ValidationExpectation.Text, "Your cart")],
            [new("checkout", "Continue to checkout", ValidationAction.Click, new(Role: "button", Name: "Checkout", Selector: "#checkout"), Expectations: [new(ValidationExpectation.Text, "Order confirmed")])],
            DateTimeOffset.UtcNow);
        var result = new PlaywrightFlowExporter().Export(flow);
        Assert.Multiple(() => { Assert.That(result.FileName, Is.EqualTo("customer-completes-checkout.spec.ts")); Assert.That(result.Content, Does.Contain("process.env.AGENT_UP_BASE_URL")); Assert.That(result.Content, Does.Contain("getByRole(\"button\", { name: \"Checkout\" })")); Assert.That(result.Content, Does.Contain("getByText(\"Order confirmed\"")); Assert.That(result.Content, Does.Not.Contain("waitForTimeout")); });
    }
}
