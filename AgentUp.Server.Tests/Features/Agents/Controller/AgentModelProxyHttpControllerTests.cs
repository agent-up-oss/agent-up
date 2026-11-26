using System.Reflection;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.Agents.Controller;

[TestFixture]
public sealed class AgentModelProxyHttpControllerTests
{
    [Test]
    public void Controller_isAnonymousAndAcceptsEveryVerb()
    {
        Assert.Multiple(() =>
        {
            Assert.That(typeof(AgentModelProxyHttpController).GetCustomAttribute<AllowAnonymousAttribute>(), Is.Not.Null);
            Assert.That(
                typeof(AgentModelProxyHttpController).GetMethod(nameof(AgentModelProxyHttpController.Forward))!
                    .GetCustomAttribute<AcceptVerbsAttribute>()!.HttpMethods,
                Does.Contain("POST").And.Contain("GET"));
        });
    }

    [Test]
    public async Task Forward_rejectsAKeyTheSandboxWasNotIssued()
    {
        var access = new AgentModelProxyAccess();
        var service = new AgentModelProxyService(
            access,
            new FixedDestinations(new AgentModelProxyTarget("https://api.anthropic.com", null)),
            new AgentModelProxyForwarder());
        var controller = new AgentModelProxyHttpController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        await controller.Forward("other", "claude", "v1/messages");

        Assert.That(controller.HttpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    private sealed class FixedDestinations(AgentModelProxyTarget target) : IAgentModelProxyDestinations
    {
        public AgentModelProxyTarget? Resolve(string capability) => target;
    }
}
