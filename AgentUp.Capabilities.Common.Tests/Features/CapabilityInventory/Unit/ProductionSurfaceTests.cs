using System.Reflection;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityInventory.Unit;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    private static readonly Type ProductionType = Assembly.Load("AgentUp.Capabilities.Common").GetType("AgentUp.Capabilities.Common.Features.CapabilityInventory.Models.DeclaredCapabilityInventoryEntry")!;

    [Test]
    public void Required_models_type_is_part_of_the_slice()
        => Assert.That(ProductionType, Is.Not.Null);

    [Test]
    public void Required_models_type_keeps_its_feature_namespace()
        => Assert.That(ProductionType.Namespace, Does.Contain(".Features.CapabilityInventory."));
}
