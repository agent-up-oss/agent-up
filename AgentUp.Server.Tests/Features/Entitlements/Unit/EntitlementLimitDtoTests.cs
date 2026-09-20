using AgentUp.Server.Features.Entitlements.DTOs;

namespace AgentUp.Server.Tests.Features.Entitlements.Unit;

[TestFixture]
public sealed class EntitlementLimitDtoTests
{
    [Test]
    public void Constructor_PreservesMaxAndUsed()
    {
        var limit = new EntitlementLimitDto(10, 3);
        Assert.Multiple(() =>
        {
            Assert.That(limit.Max, Is.EqualTo(10));
            Assert.That(limit.Used, Is.EqualTo(3));
        });
    }
}
