using System.Net;
using AgentUp.Server.Features.Authentication.Interfaces;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class McpNetworkRestrictionMiddleware : IMcpNetworkRestrictionMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/mcp")
            && context.Connection.RemoteIpAddress is { } remote
            && !IPAddress.IsLoopback(remote))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    }
}
