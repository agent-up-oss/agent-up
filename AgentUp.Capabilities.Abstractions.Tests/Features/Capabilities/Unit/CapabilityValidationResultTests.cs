using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Abstractions.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityValidationResultTests
{
    [Test]
    public void Failure_marksCapabilityAsUnableToRun()
    {
        var result = CapabilityValidationResult.Failure(
            new CapabilityValidationMessage("missing", "Missing dependency.", CapabilityValidationSeverity.Error));

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single().Code, Is.EqualTo("missing"));
    }
}
