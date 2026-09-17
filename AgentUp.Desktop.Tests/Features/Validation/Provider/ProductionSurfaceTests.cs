using System.Reflection;

namespace AgentUp.Desktop.Tests.Features.Validation.Provider;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Desktop.Features.Validation.Providers.ValidationFlowApiClient")]
    public void Required_providers_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Desktop");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
