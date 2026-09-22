using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Common.Tests.Features.SdkCommon.Unit;

[TestFixture]
public sealed class CapabilityKindTests
{
    [Test]
    public void Allowed_contains_runtime_and_agent()
    {
        Assert.That(CapabilityKind.Allowed, Does.Contain(CapabilityKind.Runtime));
        Assert.That(CapabilityKind.Allowed, Does.Contain(CapabilityKind.Agent));
    }

    [Test]
    public void Allowed_is_a_closed_pair()
    {
        Assert.That(CapabilityKind.Allowed, Has.Count.EqualTo(2));
    }
}
