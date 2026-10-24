using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentUnavailabilityTests
{
    [Test]
    public void CapabilityNotEnabled_namesThePackage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AgentUnavailability.CapabilityNotEnabledDetail("Claude"),
                Is.EqualTo("Claude is not enabled on this Server."));
            Assert.That(
                AgentUnavailability.CapabilityNotEnabledRemedy("claude"),
                Is.EqualTo("Enable the claude capability."));
        });
    }

    [Test]
    public void ExecutableNotFound_namesTheCommandWhenItIsKnown()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AgentUnavailability.ExecutableNotFoundDetail("claude"),
                Is.EqualTo("claude ACP executable is not installed or is not on PATH."));
            Assert.That(
                AgentUnavailability.ExecutableNotFoundRemedy("claude-agent-acp", "claude"),
                Is.EqualTo("Install claude-agent-acp and keep it on PATH."));
        });
    }

    [Test]
    public void ExecutableNotFound_fallsBackToTheAgentIdWhenTheCommandIsUnknown()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AgentUnavailability.ExecutableNotFoundRemedy(null, "claude"),
                Is.EqualTo("Install the claude ACP executable and keep it on PATH."));
            Assert.That(
                AgentUnavailability.ExecutableNotFoundRemedy("  ", "claude"),
                Is.EqualTo("Install the claude ACP executable and keep it on PATH."));
        });
    }
}
