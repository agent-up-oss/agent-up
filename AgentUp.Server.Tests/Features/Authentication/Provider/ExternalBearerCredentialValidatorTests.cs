using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
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

    [Test]
    public void Validate_AcceptsRsaTokenWithConfiguredPublicKey()
    {
        using var rsa = RSA.Create(2048);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", rsa.ExportSubjectPublicKeyInfoPem()));
        var validator = CreateValidator(settings);

        Assert.That(validator.Validate(IssueRsaToken(rsa)), Is.Not.Null);
    }

    [Test]
    public void Validate_AcceptsRsaTokenFromJwks()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "key-1" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/.well-known/jwks.json"));
        var handler = new StubHttpMessageHandler($"{{\"keys\":[{System.Text.Json.JsonSerializer.Serialize(jwk)}]}}");
        var validator = CreateValidator(settings, new HttpClient(handler));

        Assert.That(validator.Validate(IssueRsaToken(rsa, key.KeyId)), Is.Not.Null);
        Assert.That(handler.RequestCount, Is.EqualTo(1));
    }

    [Test]
    public void Validate_AcceptsEcTokenWithConfiguredPublicKey()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", ec.ExportSubjectPublicKeyInfoPem()));
        var validator = CreateValidator(settings);

        Assert.That(validator.Validate(IssueEcToken(ec)), Is.Not.Null);
    }

    [Test]
    public void Validate_RejectsAlgorithmsFromTheWrongKeyMode()
    {
        using var rsa = RSA.Create(2048);
        var asymmetric = CreateValidator(Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", rsa.ExportSubjectPublicKeyInfoPem())));

        Assert.Multiple(() =>
        {
            Assert.That(asymmetric.Validate(IssueToken()), Is.Null);
            Assert.That(CreateValidator().Validate(IssueRsaToken(rsa)), Is.Null);
        });
    }

    [Test]
    public void Validate_RejectsUnsignedTokens()
    {
        var token = new JwtSecurityToken(
            "https://issuer.test",
            "environment-1",
            [new Claim("sub", "user-1")],
            expires: DateTime.UtcNow.AddMinutes(5));

        Assert.That(CreateValidator().Validate(new JwtSecurityTokenHandler().WriteToken(token)), Is.Null);
    }

    [Test]
    public void Validate_HonorsExplicitAlgorithmAllowlist()
    {
        using var rsa = RSA.Create(2048);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", rsa.ExportSubjectPublicKeyInfoPem()));
        var token = IssueRsaToken(rsa, algorithm: SecurityAlgorithms.RsaSha384);

        Assert.Multiple(() =>
        {
            Assert.That(CreateValidator(settings).Validate(token), Is.Null);
            settings["AGENTUP_EXTERNAL_ALGORITHMS"] = SecurityAlgorithms.RsaSha384;
            Assert.That(CreateValidator(settings).Validate(token), Is.Not.Null);
        });
    }

    [Test]
    public void Constructor_RejectsAmbiguousKeyConfiguration()
    {
        var settings = Settings(("AGENTUP_EXTERNAL_PUBLIC_KEY", "unused"));
        var exception = Assert.Throws<InvalidOperationException>(() => CreateValidator(settings));

        Assert.That(exception!.Message, Does.Contain("exactly one external bearer verification source"));
    }

    [Test]
    public void Validate_RateLimitsUnknownJwksKeyRefreshes()
    {
        using var published = RSA.Create(2048);
        using var unknown = RSA.Create(2048);
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(
            new RsaSecurityKey(published) { KeyId = "published" });
        var handler = new StubHttpMessageHandler(Jwks(jwk));
        var validator = CreateValidator(Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/keys")), new HttpClient(handler));

        Assert.Multiple(() =>
        {
            Assert.That(validator.Validate(IssueRsaToken(unknown, "missing-1")), Is.Null);
            Assert.That(validator.Validate(IssueRsaToken(unknown, "missing-2")), Is.Null);
            Assert.That(handler.RequestCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Validate_FailsClosedWhenJwksIsUnavailableAndCacheIsEmpty()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        var validator = CreateValidator(Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/keys")), new HttpClient(handler));

        Assert.That(validator.Validate(IssueToken()), Is.Null);
    }

    [Test]
    public void Validate_RefreshesRotatedJwksAndDropsRetiredKeys()
    {
        using var retired = RSA.Create(2048);
        using var current = RSA.Create(2048);
        var retiredJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(
            new RsaSecurityKey(retired) { KeyId = "retired" });
        var currentJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(
            new RsaSecurityKey(current) { KeyId = "current" });
        var handler = new StubHttpMessageHandler(Jwks(retiredJwk), Jwks(currentJwk));
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/keys"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var keys = new ExternalBearerSigningKeyProvider(configuration, new HttpClient(handler), clock);
        var validator = new ExternalBearerCredentialValidator(configuration, keys);

        Assert.That(validator.Validate(IssueRsaToken(retired, "retired")), Is.Not.Null);
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(validator.Validate(IssueRsaToken(current, "current")), Is.Not.Null);
            Assert.That(validator.Validate(IssueRsaToken(retired, "retired")), Is.Null);
            Assert.That(handler.RequestCount, Is.EqualTo(2));
        });
    }

    private static ExternalBearerCredentialValidator CreateValidator(params (string Key, string Value)[] values)
    {
        var settings = values.Length == 0 ? Settings() : values.ToDictionary(value => value.Key, value => (string?)value.Value);
        return CreateValidator(settings);
    }

    private static Dictionary<string, string?> Settings(params (string Key, string? Value)[] overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AGENTUP_EXTERNAL_ISSUER"] = "https://issuer.test",
            ["AGENTUP_EXTERNAL_AUDIENCE"] = "environment-1",
            ["AGENTUP_EXTERNAL_SIGNING_KEY"] = SigningKey
        };
        foreach (var (key, value) in overrides)
            settings[key] = value;
        return settings;
    }

    private static ExternalBearerCredentialValidator CreateValidator(
        Dictionary<string, string?> settings,
        HttpClient? client = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var keys = new ExternalBearerSigningKeyProvider(configuration, client ?? new HttpClient());
        return new ExternalBearerCredentialValidator(configuration, keys);
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

    private static string IssueRsaToken(
        RSA rsa,
        string? keyId = null,
        string algorithm = SecurityAlgorithms.RsaSha256)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };
        var token = new JwtSecurityToken(
            "https://issuer.test",
            "environment-1",
            [new Claim("sub", "user-1")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string IssueEcToken(ECDsa ec)
    {
        var token = new JwtSecurityToken(
            "https://issuer.test",
            "environment-1",
            [new Claim("sub", "user-1")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new ECDsaSecurityKey(ec), SecurityAlgorithms.EcdsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Jwks(JsonWebKey key)
        => $"{{\"keys\":[{System.Text.Json.JsonSerializer.Serialize(key)}]}}";

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(params string[] responses)
        {
            _responses = new Queue<string>(responses);
            _statusCode = HttpStatusCode.OK;
        }

        public StubHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
            _responses = new Queue<string>();
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = _responses.Count > 1 ? _responses.Dequeue() : _responses.FirstOrDefault();
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = response is null ? null : new StringContent(response, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
