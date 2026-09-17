using System.Reflection;

namespace AgentUp.Desktop.Tests.Features.Database.Controller;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Desktop.Features.Database.Controllers.DatabaseController")]
    public void Required_controllers_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Desktop");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
