using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class ExternalBearerCredentialValidatorTests
{
    private const string SigningKey = "unit-test-signing-key-32-bytes!!";

    [Test]
    public void Validate_AcceptsSignedTokenForConfiguredIssuerAndAudience()
    {
        var validator = CreateValidator();
        var principal = validator.Validate(IssueToken(workspace: "ws-1", permissions: [OperationPermissions.WorkspaceRead]));

        Assert.Multiple(() =>
        {
            Assert.That(principal, Is.Not.Null);
            Assert.That(principal!.Subject, Is.EqualTo("user-1"));
            Assert.That(principal.Workspace, Is.EqualTo("ws-1"));
            Assert.That(principal.Permissions, Does.Contain(OperationPermissions.WorkspaceRead));
        });
    }

    [Test]
    public void Validate_RejectsTokenForADifferentAudience()
    {
        var validator = CreateValidator();
        var token = IssueToken(audience: "other-environment");
        Assert.That(validator.Validate(token), Is.Null);
    }

    [Test]
    public void Validate_ReturnsNullWhenExternalIssuerConfigurationIsIncomplete()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CreateValidator(("AGENTUP_EXTERNAL_ISSUER", "https://issuer.test")).Validate(IssueToken()), Is.Null);
            Assert.That(CreateValidator(
                ("AGENTUP_EXTERNAL_ISSUER", "https://issuer.test"),
                ("AGENTUP_EXTERNAL_AUDIENCE", "environment-1")).Validate(IssueToken()), Is.Null);
            Assert.That(CreateValidator(
                ("AGENTUP_EXTERNAL_ISSUER", "https://issuer.test"),
                ("AGENTUP_EXTERNAL_SIGNING_KEY", SigningKey)).Validate(IssueToken()), Is.Null);
        });
    }

    [Test]
    public void Validate_RejectsMissingOrMalformedTokens()
    {
        var validator = CreateValidator();
        Assert.Multiple(() =>
        {
            Assert.That(validator.Validate(null), Is.Null);
            Assert.That(validator.Validate("   "), Is.Null);
            Assert.That(validator.Validate("not-a-jwt"), Is.Null);
        });
    }

    [Test]
    public void Validate_ReadsTenantAndNameIdentifierSubject()
    {
        var original = JwtSecurityTokenHandler.DefaultMapInboundClaims;
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        try
        {
            var principal = CreateValidator().Validate(IssueToken(
                subjectClaimType: ClaimTypes.NameIdentifier,
                tenant: "ten-1"));
            Assert.Multiple(() =>
            {
                Assert.That(principal, Is.Not.Null);
                Assert.That(principal!.Subject, Is.EqualTo("user-1"));
                Assert.That(principal.Tenant, Is.EqualTo("ten-1"));
            });
        }
        finally
        {
            JwtSecurityTokenHandler.DefaultMapInboundClaims = original;
        }
    }

    [Test]
    public void Validate_RejectsTokenWithoutASubject()
    {
        Assert.That(CreateValidator().Validate(IssueToken(includeSubject: false)), Is.Null);
    }

    private static ExternalBearerCredentialValidator CreateValidator(params (string Key, string Value)[] values)
    {
        var settings = values.Length == 0
            ? new Dictionary<string, string?>
            {
                ["AGENTUP_EXTERNAL_ISSUER"] = "https://issuer.test",
                ["AGENTUP_EXTERNAL_AUDIENCE"] = "environment-1",
                ["AGENTUP_EXTERNAL_SIGNING_KEY"] = SigningKey
            }
            : values.ToDictionary(value => value.Key, value => (string?)value.Value);
        return new ExternalBearerCredentialValidator(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private static string IssueToken(
        string audience = "environment-1",
        string subjectClaimType = "sub",
        string? tenant = null,
        string? workspace = null,
        bool includeSubject = true,
        IReadOnlyList<string>? permissions = null)
    {
        var claims = new List<Claim>();
        if (includeSubject)
            claims.Add(new Claim(subjectClaimType, "user-1"));
        if (tenant is not null)
            claims.Add(new Claim("tenant", tenant));
        if (workspace is not null)
            claims.Add(new Claim("workspace", workspace));
        foreach (var permission in permissions ?? [])
            claims.Add(new Claim("permissions", permission));

        var token = new JwtSecurityToken(
            "https://issuer.test",
            audience,
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
