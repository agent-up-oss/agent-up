using System.Security.Claims;
using System.Text.Encodings.Web;
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
    public const string Scheme = "AgentUpBearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!authentication.IsRequired)
            return Task.FromResult(Success());

        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !authentication.IsAuthenticated(authorization[prefix.Length..]))
            return Task.FromResult(AuthenticateResult.NoResult());

        return Task.FromResult(Success());
    }

    private AuthenticateResult Success()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme));
    }
}
