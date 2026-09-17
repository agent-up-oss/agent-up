using System.Reflection;

namespace AgentUp.Server.Tests.Features.Ports.Controller;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    private static readonly Type ProductionType = Assembly.Load("AgentUp.Server").GetType("AgentUp.Server.Features.Ports.Controllers.PortsController")!;

    [Test]
    public void Required_controllers_type_is_part_of_the_slice()
        => Assert.That(ProductionType, Is.Not.Null);

    [Test]
    public void Required_controllers_type_keeps_its_feature_namespace()
        => Assert.That(ProductionType.Namespace, Does.Contain(".Features.Ports."));
}
