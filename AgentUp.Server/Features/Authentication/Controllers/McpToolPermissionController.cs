using AgentUp.Server.Features.Authentication.Services;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Controllers;

public sealed class McpToolPermissionController(McpToolPermissionService permissions)
{
    public void Restrict(HttpContext context, McpServerOptions options)
        => permissions.Restrict(context.User, options);

    public void RestrictTo(McpServerOptions options, IReadOnlySet<string> granted)
        => permissions.RestrictTo(options, granted);

    /// <summary>The operation permission a tool needs, or null when the tool is unmapped.</summary>
    public string? RequiredPermission(string tool)
        => permissions.RequiredPermission(tool);
}
