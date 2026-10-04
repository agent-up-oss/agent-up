using AgentUp.Server.Features.Authentication.Services;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Controllers;

public sealed class McpToolPermissionController(McpToolPermissionService permissions)
{
    public void Restrict(HttpContext context, McpServerOptions options)
        => permissions.Restrict(context.User, options);
}
