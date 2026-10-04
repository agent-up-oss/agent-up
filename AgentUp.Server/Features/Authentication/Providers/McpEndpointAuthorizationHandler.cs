using System.Net;
using AgentUp.Server.Features.Authentication.Models;
using Microsoft.AspNetCore.Authorization;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class McpEndpointAuthorizationHandler(IHttpContextAccessor contexts)
    : AuthorizationHandler<McpLoopbackOrPermissionRequirement>
{
    public const string PermissionClaimType = "permissions";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        McpLoopbackOrPermissionRequirement requirement)
    {
        if (IsLoopbackCaller() || context.User.HasClaim(PermissionClaimType, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    /// <summary>
    /// A connection the Server cannot attribute to a remote address is treated the same way
    /// <see cref="McpNetworkRestrictionMiddleware"/> treats it, so one decision cannot say
    /// loopback while the other says remote.
    /// </summary>
    private bool IsLoopbackCaller()
        => contexts.HttpContext is { } http
           && (http.Connection.RemoteIpAddress is not { } remote || IPAddress.IsLoopback(remote));
}
