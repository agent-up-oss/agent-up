using AgentUp.Server.Features.Authentication.Controllers;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Services;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Authentication.Controller;

[TestFixture]
public sealed class McpWorkspaceBindingControllerTests
{
    [Test]
    public void Pin_ForwardsABoundHttpUserToTheService()
    {
        var filters = new RecordingFilters();
        var controller = new McpWorkspaceBindingController(new McpWorkspaceBindingService(filters));
        var context = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim("workspace", "ws-a")]))
        };

        controller.Pin(context, new McpServerOptions());

        Assert.That(filters.BoundWorkspace, Is.EqualTo("ws-a"));
    }

    [Test]
    public void Pin_DoesNotAttachFiltersForAnUnboundUser()
    {
        var filters = new RecordingFilters();
        var controller = new McpWorkspaceBindingController(new McpWorkspaceBindingService(filters));

        controller.Pin(new DefaultHttpContext(), new McpServerOptions());

        Assert.That(filters.BoundWorkspace, Is.Null);
    }

    private sealed class RecordingFilters : IMcpWorkspaceBindingFilterProvider
    {
        public string? BoundWorkspace { get; private set; }

        public void Attach(McpServerOptions options, string boundWorkspace)
            => BoundWorkspace = boundWorkspace;
    }
}
