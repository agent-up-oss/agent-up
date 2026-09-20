using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Models;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class ExternalBearerCredentialValidator : ICredentialValidator
{
    private readonly TokenValidationParameters? _parameters;

    public ExternalBearerCredentialValidator(IConfiguration configuration)
    {
        var issuer = configuration["AGENTUP_EXTERNAL_ISSUER"];
        var audience = configuration["AGENTUP_EXTERNAL_AUDIENCE"];
        var signingKey = configuration["AGENTUP_EXTERNAL_SIGNING_KEY"];
        if (string.IsNullOrWhiteSpace(issuer)
            || string.IsNullOrWhiteSpace(audience)
            || string.IsNullOrWhiteSpace(signingKey))
        {
            _parameters = null;
            return;
        }

        _parameters = new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    public AuthenticatedPrincipal? Validate(string? token)
    {
        if (_parameters is null || string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, _parameters, out _);
            var subject = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(subject))
                return null;

            var permissions = principal.FindAll("permissions").Select(claim => claim.Value).ToArray();
            return new AuthenticatedPrincipal(
                subject,
                principal.FindFirst("tenant")?.Value,
                principal.FindFirst("workspace")?.Value,
                permissions);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }
}
