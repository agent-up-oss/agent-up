using System.Reflection;

namespace AgentUp.CLI.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.CLI.Features.Authentication.Services.AuthenticationCommandService")]
    public void Required_services_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.CLI");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
