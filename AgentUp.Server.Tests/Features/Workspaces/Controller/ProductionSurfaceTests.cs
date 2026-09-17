using System.Reflection;

namespace AgentUp.Server.Tests.Features.Workspaces.Controller;

[TestFixture]
public sealed class ProductionSurfaceTests
{
    [TestCase("AgentUp.Server.Features.Workspaces.Controllers.WorkspaceLifecycleController")]
    [TestCase("AgentUp.Server.Features.Workspaces.Controllers.WorkspaceQueryController")]
    public void Required_controllers_type_is_part_of_the_slice(string typeName)
    {
        var productionAssembly = Assembly.Load("AgentUp.Server");

        Assert.That(productionAssembly.GetType(typeName), Is.Not.Null);
    }
}
