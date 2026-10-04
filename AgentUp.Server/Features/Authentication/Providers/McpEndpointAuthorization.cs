using AgentUp.Server.Features.Authentication.Models;
using Microsoft.AspNetCore.Authorization;

namespace AgentUp.Server.Features.Authentication.Providers;

public static class McpEndpointAuthorization
{
    public static void Configure(AuthorizationOptions options)
    {
        foreach (var permission in McpEndpointPermissions.Permissions)
        {
            options.AddPolicy(
                McpEndpointPermissions.PolicyName(permission),
                policy => policy.AddRequirements(new McpLoopbackOrPermissionRequirement(permission)));
        }
    }
}
