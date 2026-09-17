using System.Reflection;

namespace AgentUp.Desktop.Tests.Features.FirstRun.Unit;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Desktop.Features.FirstRun.Services.FileFirstRunTutorialSettingsStore")]
    [TestCase("AgentUp.Desktop.Features.FirstRun.Services.FirstRunCheckResult")]
    public void Required_services_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Desktop");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
