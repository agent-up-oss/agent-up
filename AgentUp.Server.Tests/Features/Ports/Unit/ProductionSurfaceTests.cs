using System.Reflection;

namespace AgentUp.Server.Tests.Features.Ports.Unit;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    private static readonly Type ProductionType = Assembly.Load("AgentUp.Server").GetType("AgentUp.Server.Features.Ports.Services.PortAllocationService")!;

    [Test]
    public void Required_services_type_is_part_of_the_slice()
        => Assert.That(ProductionType, Is.Not.Null);

    [Test]
    public void Required_services_type_keeps_its_feature_namespace()
        => Assert.That(ProductionType.Namespace, Does.Contain(".Features.Ports."));
}
