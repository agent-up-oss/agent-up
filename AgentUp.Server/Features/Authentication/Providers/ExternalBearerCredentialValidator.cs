using System.IdentityModel.Tokens.Jwt;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Models;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class ExternalBearerCredentialValidator : ICredentialValidator
{
    private readonly TokenValidationParameters? _parameters;
    private readonly ExternalBearerSigningKeyProvider _signingKeys;

    public ExternalBearerCredentialValidator(
        IConfiguration configuration,
        ExternalBearerSigningKeyProvider signingKeys)
    {
        _signingKeys = signingKeys;
        var issuer = configuration["AGENTUP_EXTERNAL_ISSUER"];
        var audience = configuration["AGENTUP_EXTERNAL_AUDIENCE"];
        if (string.IsNullOrWhiteSpace(issuer)
            || string.IsNullOrWhiteSpace(audience)
            || !signingKeys.IsConfigured)
        {
            _parameters = null;
            return;
        }

        _parameters = new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKeyResolver = (_, _, keyId, _) => signingKeys.Resolve(keyId),
            ValidAlgorithms = signingKeys.Algorithms,
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
            var principal = ValidateToken(token);
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
        catch (Exception ex) when (ex is SecurityTokenException
            or ArgumentException
            or HttpRequestException
            or System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private System.Security.Claims.ClaimsPrincipal ValidateToken(string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token, _parameters!, out _);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            var refreshed = _parameters!.Clone();
            refreshed.IssuerSigningKeyResolver = (_, _, keyId, _) =>
                _signingKeys.Resolve(keyId, refreshOnUnknownKey: true);
            return new JwtSecurityTokenHandler().ValidateToken(token, refreshed, out _);
        }
    }
}
