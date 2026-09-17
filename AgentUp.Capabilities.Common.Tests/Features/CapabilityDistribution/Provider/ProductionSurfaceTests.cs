using System.Reflection;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityDistribution.Provider;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers.CapabilityChecksumProvider")]
    public void Required_providers_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Capabilities.Common");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
