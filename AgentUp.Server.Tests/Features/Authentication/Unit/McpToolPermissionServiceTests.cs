using System.Security.Claims;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Services;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class McpToolPermissionServiceTests
{
    [Test]
    public void Restrict_AppliesTheGrantedPermissionsOfAScopedToken()
    {
        var filters = new RecordingFilters();
        var user = Principal(OperationPermissions.GitRead, OperationPermissions.WorkspaceRead);

        new McpToolPermissionService(filters).Restrict(user, new McpServerOptions());

        Assert.That(filters.Granted, Is.EquivalentTo(new[] { "git.read", "workspace.read" }));
    }

    /// <summary>
    /// Loopback anonymous, local administrator, and an auth-disabled Server all arrive with no
    /// permissions claim. None of them is a caller with zero permissions, so the session keeps
    /// the full per-endpoint toolset it has today.
    /// </summary>
    [Test]
    public void Restrict_LeavesAnUnscopedPrincipalWithTheFullEndpointToolset()
    {
        var filters = new RecordingFilters();

        new McpToolPermissionService(filters).Restrict(new ClaimsPrincipal(new ClaimsIdentity()), new McpServerOptions());

        Assert.That(filters.Granted, Is.Null);
    }

    [Test]
    public void Restrict_IgnoresBlankPermissionClaims()
    {
        var filters = new RecordingFilters();
        var user = Principal(" ", string.Empty);

        new McpToolPermissionService(filters).Restrict(user, new McpServerOptions());

        Assert.That(filters.Granted, Is.Null);
    }

    private static ClaimsPrincipal Principal(params string[] permissions)
        => new(new ClaimsIdentity(permissions.Select(permission => new Claim("permissions", permission))));

    private sealed class RecordingFilters : IMcpToolPermissionFilterProvider
    {
        public IReadOnlySet<string>? Granted { get; private set; }

        public void Apply(McpServerOptions options, IReadOnlySet<string> grantedPermissions)
            => Granted = grantedPermissions;
    }
}
