using AgentUp.Server.Features.Authentication.Models;
using Microsoft.AspNetCore.Authorization;

namespace AgentUp.Server.Features.Authentication.Controllers;

/// <summary>
/// Maps the named MCP servers. With remote access off the endpoints stay anonymous exactly as
/// they have always been; with it on each endpoint requires its permission floor, and loopback
/// anonymity is preserved inside the policy rather than by skipping authorization.
/// </summary>
public static class McpEndpointMapping
{
    public static void Map(IEndpointRouteBuilder endpoints, bool remoteEnabled)
    {
        foreach (var endpoint in McpEndpointPermissions.All)
            MapEndpoint(endpoints, endpoint, remoteEnabled);
    }

    private static void MapEndpoint(
        IEndpointRouteBuilder endpoints,
        McpEndpointPermission endpoint,
        bool remoteEnabled)
    {
        var route = endpoints.MapMcp(endpoint.Path);
        if (remoteEnabled)
            route.RequireAuthorization(McpEndpointPermissions.PolicyName(endpoint.Permission));
        else
            route.WithMetadata(new AllowAnonymousAttribute());
    }
}
