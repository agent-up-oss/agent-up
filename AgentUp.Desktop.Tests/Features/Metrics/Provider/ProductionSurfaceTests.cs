using System.Reflection;

namespace AgentUp.Desktop.Tests.Features.Metrics.Provider;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Desktop.Features.Metrics.Providers.HostMetricsApiClient")]
    public void Required_providers_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Desktop");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
