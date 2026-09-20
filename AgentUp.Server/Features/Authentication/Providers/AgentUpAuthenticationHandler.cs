using System.Security.Claims;
using System.Text.Encodings.Web;
using AgentUp.Server.Features.Authentication.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class AgentUpAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    CredentialValidationService credentials)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentUpBearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        var token = authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..]
            : WebSocketAuthenticationProtocol.ReadToken(
                Request.HttpContext.WebSockets.IsWebSocketRequest,
                Request.Headers.SecWebSocketProtocol.ToString());
        var principal = credentials.Validate(token);
        if (principal is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, principal.Subject),
            new(ClaimTypes.NameIdentifier, principal.Subject)
        };
        if (!string.IsNullOrWhiteSpace(principal.Tenant))
            claims.Add(new Claim("tenant", principal.Tenant));
        if (!string.IsNullOrWhiteSpace(principal.Workspace))
            claims.Add(new Claim("workspace", principal.Workspace));
        claims.AddRange(principal.Permissions.Select(permission => new Claim("permissions", permission)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
