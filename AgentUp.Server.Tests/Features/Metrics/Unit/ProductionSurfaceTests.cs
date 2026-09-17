using System.Reflection;

namespace AgentUp.Server.Tests.Features.Metrics.Unit;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Server.Features.Metrics.Services.HostMetricsService")]
    public void Required_services_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Server");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
