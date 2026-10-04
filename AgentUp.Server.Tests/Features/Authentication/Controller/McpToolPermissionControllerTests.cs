using System.Security.Claims;
using AgentUp.Server.Features.Authentication.Controllers;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Services;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Authentication.Controller;

[TestFixture]
public sealed class McpToolPermissionControllerTests
{
    [Test]
    public void Restrict_ForwardsTheHttpUsersPermissionsToTheService()
    {
        var filters = new RecordingFilters();
        var controller = new McpToolPermissionController(new McpToolPermissionService(filters));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("permissions", OperationPermissions.BrowserControl)]))
        };

        controller.Restrict(context, new McpServerOptions());

        Assert.That(filters.Granted, Is.EquivalentTo(new[] { "browser.control" }));
    }

    [Test]
    public void Restrict_LeavesAnUnscopedHttpUserAlone()
    {
        var filters = new RecordingFilters();
        var controller = new McpToolPermissionController(new McpToolPermissionService(filters));

        controller.Restrict(new DefaultHttpContext(), new McpServerOptions());

        Assert.That(filters.Granted, Is.Null);
    }

    private sealed class RecordingFilters : IMcpToolPermissionFilterProvider
    {
        public IReadOnlySet<string>? Granted { get; private set; }

        public void Apply(McpServerOptions options, IReadOnlySet<string> grantedPermissions)
            => Granted = grantedPermissions;
    }
}
