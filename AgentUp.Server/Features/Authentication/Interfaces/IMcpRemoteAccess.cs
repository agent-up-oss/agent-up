namespace AgentUp.Server.Features.Authentication.Interfaces;

/// <summary>
/// Whether this Server accepts MCP connections from callers that are not on the Server host.
/// Loopback MCP is always anonymous; remote MCP is opt-in and always authenticated.
/// </summary>
public interface IMcpRemoteAccess
{
    bool IsEnabled { get; }
}
