using System.Security.Claims;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Providers;
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

    /// <summary>
    /// Narrows a session to a grant that does not come from the caller's token, such as the
    /// workspace's team member kind. Like the token path it only removes tools.
    /// </summary>
    public void RestrictTo(McpServerOptions options, IReadOnlySet<string> granted)
        => filters.Apply(options, granted);

    public string? RequiredPermission(string tool)
        => McpToolPermissionMap.For(tool);
}
