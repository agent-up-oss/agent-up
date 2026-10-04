using System.Security.Claims;
using AgentUp.Server.Features.Authentication.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Services;

public sealed class McpWorkspaceBindingService(IMcpWorkspaceBindingFilterProvider filters)
{
    public void Pin(ClaimsPrincipal user, McpServerOptions options)
    {
        var boundWorkspace = user.FindFirst("workspace")?.Value;
        if (string.IsNullOrWhiteSpace(boundWorkspace))
            return;

        filters.Attach(options, boundWorkspace);
    }
}
