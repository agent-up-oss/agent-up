using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Common.Tests.Features.SdkCommon.Unit;

[TestFixture]
public sealed class CapabilityKindTests
{
    [Test]
    public void Allowed_contains_runtime_agent_and_forge()
    {
        Assert.That(CapabilityKind.Allowed, Does.Contain(CapabilityKind.Runtime));
        Assert.That(CapabilityKind.Allowed, Does.Contain(CapabilityKind.Agent));
        Assert.That(CapabilityKind.Allowed, Does.Contain(CapabilityKind.Forge));
    }

    [Test]
    public void Allowed_is_a_closed_triple()
    {
        Assert.That(CapabilityKind.Allowed, Has.Count.EqualTo(3));
    }
}
