using System.Security.Claims;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Authentication.Interfaces;

namespace AgentUp.Server.Features.Authentication.Providers;

/// <summary>
/// Turns the session credential an ACP agent presents into a caller bound to its workspace, so
/// the MCP endpoints apply the workspace pin, the endpoint permission floors and the team member
/// grant to it exactly as they do to a workspace-bound bearer token. A credential that is not live is answered like any other
/// caller that may not use MCP: <c>404</c>.
/// </summary>
public sealed class AgentMcpSessionMiddleware(AgentMcpSessionController sessions) : IAgentMcpSessionMiddleware
{
    public const string HeaderName = "X-Agent-Up-Agent-Session";
    public const string AuthenticationType = "AgentSession";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp")
            || !context.Request.Headers.TryGetValue(HeaderName, out var presented))
        {
            await next(context);
            return;
        }

        var session = sessions.Resolve(presented.ToString());
        if (session is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // The member's permissions as claims: the endpoint floors and the tool filter then treat
        // the agent exactly as they treat a bearer token that carries them.
        // A subject is required: the MCP SDK binds each stateful MCP session to its caller.
        Claim[] claims =
        [
            new("sub", $"agent-session:{session.WorkspaceId}"),
            new("workspace", session.WorkspaceId),
            .. session.Permissions.Select(permission => new Claim(McpEndpointAuthorizationHandler.PermissionClaimType, permission))
        ];
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType));
        await next(context);
    }
}
