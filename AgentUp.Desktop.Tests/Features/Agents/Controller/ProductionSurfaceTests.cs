using System.Reflection;

namespace AgentUp.Desktop.Tests.Features.Agents.Controller;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Desktop.Features.Agents.Controllers.AgentsController")]
    public void Required_controllers_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Desktop");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
