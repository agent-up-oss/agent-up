using AgentUp.Server.Features.Authentication.DTOs;
using ModelContextProtocol.Authentication;

namespace AgentUp.Server.Features.Authentication.Providers;

/// <summary>
/// Builds the RFC 9728 protected resource metadata Agent-Up publishes when remote MCP access
/// is enabled. Agent-Up is the resource server, never the authorization server, so the only
/// authorization server advertised is the configured external issuer. The resource identifier
/// is deliberately left unset: the MCP authentication handler derives it from the incoming
/// request, which is what keeps a product hostname out of the Server.
/// </summary>
public sealed class McpProtectedResourceMetadataProvider
{
    private readonly string? _authorizationServer;

    public McpProtectedResourceMetadataProvider(IConfiguration configuration)
    {
        var issuer = configuration["AGENTUP_EXTERNAL_ISSUER"];
        // A bare Unix path parses as an absolute file URI, so the scheme has to be checked too.
        _authorizationServer = Uri.TryCreate(issuer, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp)
            ? parsed.ToString()
            : null;
    }

    public bool IsConfigured => _authorizationServer is not null;

    public ProtectedResourceMetadata Create()
    {
        if (_authorizationServer is null)
        {
            throw new InvalidOperationException(
                "AGENTUP_EXTERNAL_ISSUER must be an absolute http or https URI before protected resource metadata can be published.");
        }

        return new ProtectedResourceMetadata
        {
            AuthorizationServers = [_authorizationServer],
            BearerMethodsSupported = ["header"],
            ScopesSupported = [.. OperationPermissions.All]
        };
    }
}
