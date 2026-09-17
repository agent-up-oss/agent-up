using System.Reflection;

namespace AgentUp.CLI.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.CLI.Features.Workspaces.Services.CurrentWorkspaceResolver")]
    [TestCase("AgentUp.CLI.Features.Workspaces.Services.WorkspaceCommandOutputService")]
    public void Required_services_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.CLI");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
