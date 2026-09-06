using AgentUp.Server.Features.Database.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.Database.Controller;

[TestFixture]
public sealed class DatabaseControllerTests
{
    [Test]
    public void Route_isScopedToWorkspaceApplication()
    {
        var route = typeof(DatabaseController).GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>().Single();
        Assert.That(route.Template, Is.EqualTo("api/workspaces/{workspaceId}/applications/{applicationName}/database"));
    }
}
