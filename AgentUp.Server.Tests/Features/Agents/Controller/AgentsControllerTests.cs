using System.Reflection;
using AgentUp.Server.Features.Agents.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using AgentUp.Server.Features.Agents.DTOs;

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

    [Test]
    public async Task Prompt_rejects_a_blank_message_before_scheduling_work()
    {
        var controller = new AgentsHttpController(null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Prompt("workspace", new AgentPromptRequest("   "));

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(controller.ModelState["message"]!.Errors.Single().ErrorMessage,
            Is.EqualTo("Message is required."));
    }
}
