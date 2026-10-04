using AgentUp.Server.Features.Authentication.Interfaces;

namespace AgentUp.Server.Features.Authentication.Providers;

/// <summary>
/// Reads whether this Server accepts MCP from a caller that is not on the Server host. The
/// default is off, so a Server that was never configured for it behaves exactly as before.
/// </summary>
/// <remarks>
/// This is read during service registration, because whether an endpoint is anonymous and
/// whether RFC 9728 metadata exists are both decided while the host is being composed. A test
/// therefore has to supply the value through <c>UseSetting</c>: a source added by
/// <c>ConfigureAppConfiguration</c> only exists after that decision has been made.
/// </remarks>
public sealed class McpRemoteAccessProvider : IMcpRemoteAccess
{
    public McpRemoteAccessProvider(IConfiguration configuration)
    {
        IsEnabled = string.Equals(
            configuration["AGENTUP_MCP_REMOTE_ENABLED"],
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    public bool IsEnabled { get; }
}
