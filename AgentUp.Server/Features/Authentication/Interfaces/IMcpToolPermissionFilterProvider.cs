using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Interfaces;

public interface IMcpToolPermissionFilterProvider
{
    void Apply(McpServerOptions options, IReadOnlySet<string> grantedPermissions);
}
