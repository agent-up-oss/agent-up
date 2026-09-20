using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Authentication.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class CredentialValidationServiceTests
{
    [Test]
    public void DisabledMode_ReturnsUnrestrictedPrincipalWithoutAToken()
    {
        var service = Create(("AGENTUP_AUTH_DISABLED", "true"));
        var principal = service.Validate(null);
        Assert.Multiple(() =>
        {
            Assert.That(principal, Is.Not.Null);
            Assert.That(principal!.Permissions, Is.EqualTo(OperationPermissions.All));
        });
    }

    [Test]
    public void LocalAdministratorMode_RejectsMissingToken()
    {
        var service = Create(("AGENTUP_ADMIN_PASSWORD", "secret"));
        Assert.That(service.Validate(null), Is.Null);
    }

    [Test]
    public void ExternalBearerMode_ValidatesSignedTokens()
    {
        const string signingKey = "unit-test-signing-key-32-bytes!!";
        var service = Create(
            ("AGENTUP_AUTH_MODE", "externalBearer"),
            ("AGENTUP_EXTERNAL_ISSUER", "https://issuer.test"),
            ("AGENTUP_EXTERNAL_AUDIENCE", "environment-1"),
            ("AGENTUP_EXTERNAL_SIGNING_KEY", signingKey));
        var jwt = new JwtSecurityToken(
            "https://issuer.test",
            "environment-1",
            [new Claim("sub", "user-1")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        var principal = service.Validate(new JwtSecurityTokenHandler().WriteToken(jwt));

        Assert.Multiple(() =>
        {
            Assert.That(principal, Is.Not.Null);
            Assert.That(principal!.Subject, Is.EqualTo("user-1"));
        });
    }

    private static CredentialValidationService Create(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(value => value.Key, value => (string?)value.Value)).Build();
        return new CredentialValidationService(
            new AuthenticationModeProvider(configuration),
            new LocalAdministratorCredentialValidator(new AuthenticationProvider(configuration)),
            new ExternalBearerCredentialValidator(configuration));
    }
}
