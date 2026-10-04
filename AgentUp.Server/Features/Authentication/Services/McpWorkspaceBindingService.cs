using System.Security.Claims;
using AgentUp.Server.Features.Authentication.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Services;

public sealed class McpWorkspaceBindingService(IMcpWorkspaceBindingFilterProvider filters)
{
    public void Pin(ClaimsPrincipal user, McpServerOptions options)
    {
        var boundWorkspace = BoundWorkspace(user);
        if (boundWorkspace is null)
            return;

        filters.Attach(options, boundWorkspace);
    }

    public string? BoundWorkspace(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("workspace")?.Value;
        return string.IsNullOrWhiteSpace(claim) ? null : claim;
    }
}
