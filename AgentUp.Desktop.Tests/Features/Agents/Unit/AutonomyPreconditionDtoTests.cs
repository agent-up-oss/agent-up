using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AutonomyPreconditionDtoTests
{
    [Test]
    public void Status_isReadyWhenTheStepIsSatisfied()
    {
        var item = new AutonomyPreconditionDto("forgeCapabilityEnabled", true);

        Assert.Multiple(() =>
        {
            Assert.That(item.Status, Is.EqualTo("Ready"));
            Assert.That(item.HasRemedy, Is.False);
            Assert.That(item.Label, Is.EqualTo("Forge capability"));
            Assert.That(item.AutomationId, Is.EqualTo(DesktopAutomationIds.AgentLoopPrecondition("forgeCapabilityEnabled")));
        });
    }

    [Test]
    public void Status_usesTheRemedyWhenTheStepIsOutstanding()
    {
        var item = new AutonomyPreconditionDto("hostConnectionAuthorized", false, "Sign in to your repository host.");

        Assert.Multiple(() =>
        {
            Assert.That(item.Status, Is.EqualTo("Sign in to your repository host."));
            Assert.That(item.HasRemedy, Is.True);
            Assert.That(item.Label, Is.EqualTo("Host connection"));
        });
    }

    [Test]
    public void Status_fallsBackWhenTheRemedyIsMissing()
    {
        var item = new AutonomyPreconditionDto("agentAvailable", false);

        Assert.That(item.Status, Is.EqualTo("Not ready"));
        Assert.That(item.HasRemedy, Is.False);
        Assert.That(item.Label, Is.EqualTo("Agent available"));
    }

    [Test]
    public void Label_usesTheIdWhenTheStepIsUnknown()
    {
        var item = new AutonomyPreconditionDto("customStep", false, "  ");

        Assert.That(item.Label, Is.EqualTo("customStep"));
        Assert.That(item.HasRemedy, Is.False);
        Assert.That(new AutonomyPreconditionDto("autonomyEnabled", false).Label, Is.EqualTo("Autonomy enabled"));
    }
}
