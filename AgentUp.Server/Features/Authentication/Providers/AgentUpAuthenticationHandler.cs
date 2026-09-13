using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class AgentUpAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthenticationProvider authentication)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AgentUpBearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!authentication.IsRequired)
            return Task.FromResult(Success());

        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        var token = authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..]
            : ReadWebSocketToken();
        if (!authentication.IsAuthenticated(token))
            return Task.FromResult(AuthenticateResult.NoResult());

        return Task.FromResult(Success());
    }

    private string? ReadWebSocketToken()
    {
        if (!Request.HttpContext.WebSockets.IsWebSocketRequest) return null;

        const string protocolPrefix = "agent-up.auth.";
        var protocol = Request.Headers.SecWebSocketProtocol.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith(protocolPrefix, StringComparison.Ordinal));
        if (protocol is null) return null;

        try
        {
            var encoded = protocol[protocolPrefix.Length..].Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private AuthenticateResult Success()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
