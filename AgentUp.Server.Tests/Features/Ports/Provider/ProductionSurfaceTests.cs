using System.Reflection;

namespace AgentUp.Server.Tests.Features.Ports.Provider;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Server.Features.Ports.Providers.FilePortRangeStore")]
    [TestCase("AgentUp.Server.Features.Ports.Providers.SocketPortAvailabilityProvider")]
    public void Required_providers_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Server");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
