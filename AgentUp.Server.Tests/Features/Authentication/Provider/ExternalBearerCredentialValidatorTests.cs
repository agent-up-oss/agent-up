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
    private static readonly HttpClient DefaultHttpClient = new();

    [Test]
    public async Task Validate_AcceptsSignedTokenForConfiguredIssuerAndAudience()
    {
        var validator = CreateValidator();
        var principal = await validator.ValidateAsync(IssueToken(workspace: "ws-1", permissions: [OperationPermissions.WorkspaceRead]));

        Assert.Multiple(() =>
        {
            Assert.That(principal, Is.Not.Null);
            Assert.That(principal!.Subject, Is.EqualTo("user-1"));
            Assert.That(principal.Workspace, Is.EqualTo("ws-1"));
            Assert.That(principal.Permissions, Does.Contain(OperationPermissions.WorkspaceRead));
        });
    }

    [Test]
    public async Task Validate_RejectsTokenForADifferentAudience()
    {
        var validator = CreateValidator();
        var token = IssueToken(audience: "other-environment");
        Assert.That(await validator.ValidateAsync(token), Is.Null);
    }

    [Test]
    public async Task Validate_ReturnsNullWhenExternalIssuerConfigurationIsIncomplete()
    {
        var issuerOnly = await CreateValidator(
            ("AGENTUP_EXTERNAL_ISSUER", "https://issuer.test")).ValidateAsync(IssueToken());
        var issuerAndAudience = await CreateValidator(
            ("AGENTUP_EXTERNAL_ISSUER", "https://issuer.test"),
            ("AGENTUP_EXTERNAL_AUDIENCE", "environment-1")).ValidateAsync(IssueToken());
        var issuerAndKey = await CreateValidator(
            ("AGENTUP_EXTERNAL_ISSUER", "https://issuer.test"),
            ("AGENTUP_EXTERNAL_SIGNING_KEY", SigningKey)).ValidateAsync(IssueToken());

        Assert.Multiple(() =>
        {
            Assert.That(issuerOnly, Is.Null);
            Assert.That(issuerAndAudience, Is.Null);
            Assert.That(issuerAndKey, Is.Null);
        });
    }

    [Test]
    public async Task Validate_RejectsMissingOrMalformedTokens()
    {
        var validator = CreateValidator();
        var missing = await validator.ValidateAsync(null);
        var blank = await validator.ValidateAsync("   ");
        var malformed = await validator.ValidateAsync("not-a-jwt");
        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.Null);
            Assert.That(blank, Is.Null);
            Assert.That(malformed, Is.Null);
        });
    }

    [Test]
    public async Task Validate_ReadsTenantAndNameIdentifierSubject()
    {
        var original = JwtSecurityTokenHandler.DefaultMapInboundClaims;
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        try
        {
            var principal = await CreateValidator().ValidateAsync(IssueToken(
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
    public async Task Validate_RejectsTokenWithoutASubject()
    {
        Assert.That(await CreateValidator().ValidateAsync(IssueToken(includeSubject: false)), Is.Null);
    }

    [Test]
    public async Task Validate_AcceptsRsaTokenWithConfiguredPublicKey()
    {
        using var rsa = RSA.Create(2048);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", rsa.ExportSubjectPublicKeyInfoPem()));
        var validator = CreateValidator(settings);

        Assert.That(await validator.ValidateAsync(IssueRsaToken(rsa)), Is.Not.Null);
    }

    [Test]
    public async Task Validate_AcceptsRsaTokenFromJwks()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "key-1" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/.well-known/jwks.json"));
        var handler = new StubHttpMessageHandler($"{{\"keys\":[{System.Text.Json.JsonSerializer.Serialize(jwk)}]}}");
        using var client = new HttpClient(handler);
        var validator = CreateValidator(settings, client);

        Assert.That(await validator.ValidateAsync(IssueRsaToken(rsa, key.KeyId)), Is.Not.Null);
        Assert.That(handler.RequestCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Validate_AcceptsEcTokenWithConfiguredPublicKey()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", ec.ExportSubjectPublicKeyInfoPem()));
        var validator = CreateValidator(settings);

        Assert.That(await validator.ValidateAsync(IssueEcToken(ec)), Is.Not.Null);
    }

    [Test]
    public async Task Validate_AcceptsP384EcTokenWithConfiguredPublicKey()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", ec.ExportSubjectPublicKeyInfoPem()));
        var validator = CreateValidator(settings);

        Assert.That(
            await validator.ValidateAsync(IssueEcToken(ec, SecurityAlgorithms.EcdsaSha384)),
            Is.Not.Null);
    }

    [Test]
    public async Task Validate_AcceptsP521EcTokenWithConfiguredPublicKey()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", ec.ExportSubjectPublicKeyInfoPem()));
        var validator = CreateValidator(settings);

        Assert.That(
            await validator.ValidateAsync(IssueEcToken(ec, SecurityAlgorithms.EcdsaSha512)),
            Is.Not.Null);
    }

    [Test]
    public async Task Validate_TreatsBlankAlgorithmAllowlistAsSourceDefault()
    {
        var settings = Settings(("AGENTUP_EXTERNAL_ALGORITHMS", " , "));
        Assert.That(await CreateValidator(settings).ValidateAsync(IssueToken()), Is.Not.Null);
    }

    [Test]
    public async Task Validate_RejectsAlgorithmsFromTheWrongKeyMode()
    {
        using var rsa = RSA.Create(2048);
        var asymmetric = CreateValidator(Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", rsa.ExportSubjectPublicKeyInfoPem())));

        var hmacWithRsa = await asymmetric.ValidateAsync(IssueToken());
        var rsaWithHmac = await CreateValidator().ValidateAsync(IssueRsaToken(rsa));
        Assert.Multiple(() =>
        {
            Assert.That(hmacWithRsa, Is.Null);
            Assert.That(rsaWithHmac, Is.Null);
        });
    }

    [Test]
    public async Task Validate_RejectsUnsignedTokens()
    {
        var token = new JwtSecurityToken(
            "https://issuer.test",
            "environment-1",
            [new Claim("sub", "user-1")],
            expires: DateTime.UtcNow.AddMinutes(5));

        Assert.That(await CreateValidator().ValidateAsync(new JwtSecurityTokenHandler().WriteToken(token)), Is.Null);
    }

    [Test]
    public async Task Validate_HonorsExplicitAlgorithmAllowlist()
    {
        using var rsa = RSA.Create(2048);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", rsa.ExportSubjectPublicKeyInfoPem()));
        var token = IssueRsaToken(rsa, algorithm: SecurityAlgorithms.RsaSha384);

        var defaultResult = await CreateValidator(settings).ValidateAsync(token);
        settings["AGENTUP_EXTERNAL_ALGORITHMS"] = SecurityAlgorithms.RsaSha384;
        var allowedResult = await CreateValidator(settings).ValidateAsync(token);
        Assert.Multiple(() =>
        {
            Assert.That(defaultResult, Is.Null);
            Assert.That(allowedResult, Is.Not.Null);
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
    public void Constructor_RejectsNonHttpsJwksUri()
    {
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "http://issuer.test/keys"));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateValidator(settings));

        Assert.That(exception!.Message, Does.Contain("absolute HTTPS URI"));
    }

    [Test]
    public void Constructor_RejectsRelativeJwksUri()
    {
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "issuer.test/keys"));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateValidator(settings));

        Assert.That(exception!.Message, Does.Contain("absolute HTTPS URI"));
    }

    [Test]
    public void Constructor_RejectsInvalidPublicKeyPem()
    {
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", "-----BEGIN PUBLIC KEY-----\nQQ==\n-----END PUBLIC KEY-----"));

        Assert.Throws<CryptographicException>(() => CreateValidator(settings));
    }

    [Test]
    public void Constructor_RejectsUnsupportedEcCurves()
    {
        using var ec = ECDsa.Create(ECCurve.CreateFromValue("1.3.132.0.10"));
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_PUBLIC_KEY", ec.ExportSubjectPublicKeyInfoPem()));

        var exception = Assert.Throws<CryptographicException>(() => CreateValidator(settings));

        Assert.That(exception!.Message, Does.Contain("unsupported EC curve"));
    }

    [Test]
    public async Task Prepare_DoesNothingWhenNoJwksSourceIsConfigured()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null))).Build();
        var keys = new ExternalBearerSigningKeyProvider(configuration, DefaultHttpClient);

        await keys.PrepareAsync("any", refreshOnUnknownKey: true);

        Assert.Multiple(() =>
        {
            Assert.That(keys.IsConfigured, Is.False);
            Assert.That(keys.Resolve("any"), Is.Empty);
            Assert.That(keys.Algorithms, Is.Empty);
        });
    }

    [Test]
    public async Task Validate_RateLimitsUnknownJwksKeyRefreshes()
    {
        using var published = RSA.Create(2048);
        using var unknown = RSA.Create(2048);
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(
            new RsaSecurityKey(published) { KeyId = "published" });
        var handler = new StubHttpMessageHandler(Jwks(jwk));
        using var client = new HttpClient(handler);
        var validator = CreateValidator(Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/keys")), client);

        var first = await validator.ValidateAsync(IssueRsaToken(unknown, "missing-1"));
        var second = await validator.ValidateAsync(IssueRsaToken(unknown, "missing-2"));
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Null);
            Assert.That(second, Is.Null);
            Assert.That(handler.RequestCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Validate_FailsClosedWhenJwksIsUnavailableAndCacheIsEmpty()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler);
        var validator = CreateValidator(Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/keys")), client);

        Assert.That(await validator.ValidateAsync(IssueToken()), Is.Null);
    }

    [Test]
    public async Task Validate_FailsClosedWhenJwksRequestTimesOut()
    {
        var handler = new TimeoutHttpMessageHandler();
        using var client = new HttpClient(handler);
        var validator = CreateValidator(
            Settings(
                ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
                ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/keys")),
            client,
            jwksFetchTimeout: TimeSpan.FromMilliseconds(20));

        Assert.That(await validator.ValidateAsync(IssueToken()), Is.Null);
    }

    [Test]
    public void Validate_PropagatesCallerCancellation()
    {
        var handler = new CallerCancellationHttpMessageHandler();
        using var client = new HttpClient(handler);
        var validator = CreateValidator(Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/keys")), client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await validator.ValidateAsync(IssueToken(), cancellation.Token));
    }

    [Test]
    public async Task Validate_RejectsExpiredKeysWhenJwksRefreshFails()
    {
        using var rsa = RSA.Create(2048);
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(
            new RsaSecurityKey(rsa) { KeyId = "key-1" });
        var handler = new FailingAfterSuccessHttpMessageHandler(Jwks(jwk));
        using var client = new HttpClient(handler);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var settings = Settings(
            ("AGENTUP_EXTERNAL_SIGNING_KEY", null),
            ("AGENTUP_EXTERNAL_JWKS_URI", "https://issuer.test/keys"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var keys = new ExternalBearerSigningKeyProvider(configuration, client, clock);
        var validator = new ExternalBearerCredentialValidator(configuration, keys);

        Assert.That(await validator.ValidateAsync(IssueRsaToken(rsa, "key-1")), Is.Not.Null);
        clock.Advance(TimeSpan.FromMinutes(5));

        var failedRefresh = await validator.ValidateAsync(IssueRsaToken(rsa, "key-1"));
        var throttledRetry = await validator.ValidateAsync(IssueRsaToken(rsa, "key-1"));
        Assert.Multiple(() =>
        {
            Assert.That(failedRefresh, Is.Null);
            Assert.That(throttledRetry, Is.Null);
            Assert.That(handler.RequestCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Validate_RefreshesRotatedJwksAndDropsRetiredKeys()
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
        using var client = new HttpClient(handler);
        var keys = new ExternalBearerSigningKeyProvider(configuration, client, clock);
        var validator = new ExternalBearerCredentialValidator(configuration, keys);

        Assert.That(await validator.ValidateAsync(IssueRsaToken(retired, "retired")), Is.Not.Null);
        clock.Advance(TimeSpan.FromMinutes(1));

        var currentResult = await validator.ValidateAsync(IssueRsaToken(current, "current"));
        var retiredResult = await validator.ValidateAsync(IssueRsaToken(retired, "retired"));
        Assert.Multiple(() =>
        {
            Assert.That(currentResult, Is.Not.Null);
            Assert.That(retiredResult, Is.Null);
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
        HttpClient? client = null,
        TimeProvider? timeProvider = null,
        TimeSpan? jwksFetchTimeout = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var keys = new ExternalBearerSigningKeyProvider(
            configuration,
            client ?? DefaultHttpClient,
            timeProvider,
            jwksFetchTimeout);
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

    private static string IssueEcToken(ECDsa ec, string algorithm = SecurityAlgorithms.EcdsaSha256)
    {
        var token = new JwtSecurityToken(
            "https://issuer.test",
            "environment-1",
            [new Claim("sub", "user-1")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new ECDsaSecurityKey(ec), algorithm));
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
            return Task.FromResult(CreateResponse(_statusCode, response));
        }

        private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string? response)
            => new(statusCode)
            {
                Content = response is null ? null : new StringContent(response, Encoding.UTF8, "application/json")
            };
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<HttpResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            await using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            return await completion.Task;
        }
    }

    private sealed class CallerCancellationHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromCanceled<HttpResponseMessage>(cancellationToken);
    }

    private sealed class FailingAfterSuccessHttpMessageHandler(string response) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requestCount++;
            return _requestCount == 1
                ? Task.FromResult(CreateSuccessResponse(response))
                : Task.FromResult(CreateUnavailableResponse());
        }

        private static HttpResponseMessage CreateSuccessResponse(string response)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };

        private static HttpResponseMessage CreateUnavailableResponse()
            => new(HttpStatusCode.ServiceUnavailable);
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
