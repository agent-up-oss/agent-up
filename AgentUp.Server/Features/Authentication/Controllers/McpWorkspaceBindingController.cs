using AgentUp.Server.Features.Authentication.Services;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Controllers;

public sealed class McpWorkspaceBindingController(McpWorkspaceBindingService binding)
{
    public void Pin(HttpContext context, McpServerOptions options)
        => binding.Pin(context.User, options);

    public string? BoundWorkspace(HttpContext context)
        => binding.BoundWorkspace(context.User);
}
