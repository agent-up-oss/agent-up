using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AutonomyPreconditionsTests
{
    [Test]
    public void Refusal_namesTheOutstandingStepAndItsRemedy()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AutonomyPreconditions.Refusal(AutonomyPreconditions.ForgeCapabilityEnabled),
                Is.EqualTo("forgeCapabilityEnabled: Run: agent-up capabilities bootstrap"));
            Assert.That(
                AutonomyPreconditions.Refusal(AutonomyPreconditions.HostConnectionAuthorized),
                Is.EqualTo("hostConnectionAuthorized: Sign in to your repository host."));
            Assert.That(
                AutonomyPreconditions.Refusal(AutonomyPreconditions.AgentAvailable),
                Is.EqualTo("agentAvailable: Enable an agent capability"));
            Assert.That(
                AutonomyPreconditions.Refusal(AutonomyPreconditions.AutonomyEnabled),
                Is.EqualTo("autonomyEnabled: Enable autonomy for this Server"));
        });
    }

    [Test]
    public void Remedy_returnsTheIdWhenTheStepIsUnknown()
    {
        Assert.That(AutonomyPreconditions.Remedy("unknownStep"), Is.EqualTo("unknownStep"));
    }
}
