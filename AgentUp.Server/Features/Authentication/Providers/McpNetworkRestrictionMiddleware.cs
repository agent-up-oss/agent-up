using System.Net;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class McpNetworkRestrictionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
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
