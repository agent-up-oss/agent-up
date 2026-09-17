using System.Reflection;

namespace AgentUp.Desktop.Tests.Features.Console.Controller;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    private static readonly Type ProductionType = Assembly.Load("AgentUp.Desktop").GetType("AgentUp.Desktop.Features.Console.Controllers.ConsoleController")!;

    [Test]
    public void Required_controllers_type_is_part_of_the_slice()
        => Assert.That(ProductionType, Is.Not.Null);

    [Test]
    public void Required_controllers_type_keeps_its_feature_namespace()
        => Assert.That(ProductionType.Namespace, Does.Contain(".Features.Console."));
}
