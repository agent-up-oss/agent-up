using System.Security.Claims;
using AgentUp.Server.Features.Authentication.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Services;

public sealed class McpToolPermissionService(IMcpToolPermissionFilterProvider filters)
{
    /// <summary>
    /// A caller with no <c>permissions</c> claim is not a caller with zero permissions: loopback
    /// anonymous, local administrator, and an auth-disabled Server all arrive that way and keep
    /// today's full per-endpoint toolset. Scoping begins only once a token says what it is for.
    /// </summary>
    public void Restrict(ClaimsPrincipal user, McpServerOptions options)
    {
        var granted = user.FindAll("permissions")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        if (granted.Count == 0)
            return;

        filters.Apply(options, granted);
    }
}
