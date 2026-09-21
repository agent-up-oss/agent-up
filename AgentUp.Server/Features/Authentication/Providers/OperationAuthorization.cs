using AgentUp.Server.Features.Authentication.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace AgentUp.Server.Features.Authentication.Providers;

public static class OperationAuthorization
{
    public static void Configure(AuthorizationOptions options)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        foreach (var permission in OperationPermissions.All)
        {
            options.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("permissions", permission));
        }
    }
}
