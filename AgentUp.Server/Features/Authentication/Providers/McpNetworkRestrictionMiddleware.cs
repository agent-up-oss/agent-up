using System.Net;
using AgentUp.Server.Features.Authentication.Interfaces;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class McpNetworkRestrictionMiddleware(IMcpRemoteAccess remoteAccess)
    : IMcpNetworkRestrictionMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp")
            || IsLoopbackCaller(context)
            || IsAuthenticatedRemoteCaller(context))
        {
            await next(context);
            return;
        }

        // 404 rather than 401: an unauthenticated remote caller must not learn that this
        // Server speaks MCP at all.
        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }

    private static bool IsLoopbackCaller(HttpContext context)
        => context.Connection.RemoteIpAddress is not { } remote || IPAddress.IsLoopback(remote);

    private bool IsAuthenticatedRemoteCaller(HttpContext context)
        => remoteAccess.IsEnabled && (context.User.Identity?.IsAuthenticated ?? false);
}
