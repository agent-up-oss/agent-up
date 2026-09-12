using System.Reflection;
using AgentUp.Server.Features.Agents.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.Agents.Controller;

[TestFixture]
public sealed class AgentsControllerTests
{
    [Test]
    public void HttpController_isWorkspaceScopedAndDoesNotOptOutOfAuthentication()
    {
        var route = typeof(AgentsHttpController).GetCustomAttribute<RouteAttribute>();
        Assert.Multiple(() => {
            Assert.That(route!.Template, Is.EqualTo("api/workspaces/{workspaceId}/agent"));
            Assert.That(typeof(AgentsHttpController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>(), Is.Null);
        });
    }
}
