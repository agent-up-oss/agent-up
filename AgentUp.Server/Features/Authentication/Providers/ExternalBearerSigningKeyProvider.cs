using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class ExternalBearerSigningKeyProvider
{
    private static readonly TimeSpan JwksCacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan JwksRefreshInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultJwksFetchTimeout = TimeSpan.FromSeconds(5);
    private readonly object _sync = new();
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _jwksFetchTimeout;
    private readonly string? _jwksUri;
    private readonly SecurityKey? _configuredKey;
    private IReadOnlyCollection<SecurityKey>? _jwksKeys;
    private DateTimeOffset _jwksExpiresAt;
    private DateTimeOffset _nextJwksRefreshAt;

    public ExternalBearerSigningKeyProvider(
        IConfiguration configuration,
        HttpClient httpClient,
        TimeProvider? timeProvider = null,
        TimeSpan? jwksFetchTimeout = null)
    {
        ValidateConfiguration(configuration);
        _httpClient = httpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _jwksFetchTimeout = jwksFetchTimeout ?? DefaultJwksFetchTimeout;
        _jwksUri = NullIfWhiteSpace(configuration["AGENTUP_EXTERNAL_JWKS_URI"]);

        var secret = NullIfWhiteSpace(configuration["AGENTUP_EXTERNAL_SIGNING_KEY"]);
        var publicKey = NullIfWhiteSpace(configuration["AGENTUP_EXTERNAL_PUBLIC_KEY"]);
        if (secret is not null)
            _configuredKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        else if (publicKey is not null)
            _configuredKey = ReadPublicKey(publicKey);

        Algorithms = ParseAlgorithms(configuration["AGENTUP_EXTERNAL_ALGORITHMS"])
            ?? DefaultAlgorithms(_configuredKey, _jwksUri);
    }

    public bool IsConfigured => _configuredKey is not null || _jwksUri is not null;

    public IReadOnlyCollection<string> Algorithms { get; }

    public static void ValidateConfiguration(IConfiguration configuration)
    {
        var jwksUri = NullIfWhiteSpace(configuration["AGENTUP_EXTERNAL_JWKS_URI"]);
        var sources = new[]
        {
            configuration["AGENTUP_EXTERNAL_SIGNING_KEY"],
            configuration["AGENTUP_EXTERNAL_PUBLIC_KEY"],
            jwksUri
        }.Count(value => !string.IsNullOrWhiteSpace(value));

        if (sources > 1)
        {
            throw new InvalidOperationException(
                "Configure exactly one external bearer verification source: "
                + "AGENTUP_EXTERNAL_SIGNING_KEY, AGENTUP_EXTERNAL_PUBLIC_KEY, or AGENTUP_EXTERNAL_JWKS_URI.");
        }

        if (jwksUri is not null
            && (!Uri.TryCreate(jwksUri, UriKind.Absolute, out var parsedUri)
                || parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("AGENTUP_EXTERNAL_JWKS_URI must be an absolute HTTPS URI.");
        }
    }

    public IEnumerable<SecurityKey> Resolve(string? keyId)
    {
        if (_configuredKey is not null)
            return [_configuredKey];
        if (_jwksUri is null)
            return [];

        lock (_sync)
        {
            if (_timeProvider.GetUtcNow() >= _jwksExpiresAt)
                return [];

            return FindKeys(_jwksKeys ?? [], keyId);
        }
    }

    public async Task PrepareAsync(
        string? keyId,
        bool refreshOnUnknownKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_jwksUri is null)
            return;

        bool shouldRefresh;
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var hasMatchingKey = FindKeys(_jwksKeys ?? [], keyId).Count > 0;
            var wantsRefresh = _jwksKeys is null
                || now >= _jwksExpiresAt
                || (refreshOnUnknownKey && !hasMatchingKey);
            shouldRefresh = wantsRefresh && now >= _nextJwksRefreshAt;
            if (shouldRefresh)
                _nextJwksRefreshAt = now.Add(JwksRefreshInterval);
        }

        if (!shouldRefresh)
            return;

        using var timeout = new CancellationTokenSource(_jwksFetchTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var json = await _httpClient.GetStringAsync(_jwksUri, linked.Token);
            var keys = new JsonWebKeySet(json).GetSigningKeys().ToArray();
            lock (_sync)
            {
                _jwksKeys = keys;
                var now = _timeProvider.GetUtcNow();
                _jwksExpiresAt = now.Add(JwksCacheLifetime);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static IReadOnlyCollection<SecurityKey> FindKeys(
        IReadOnlyCollection<SecurityKey> keys,
        string? keyId)
        => string.IsNullOrWhiteSpace(keyId) ? keys : keys.Where(key => key.KeyId == keyId).ToArray();

    private static SecurityKey ReadPublicKey(string pem)
    {
        pem = pem.Replace("\\n", "\n", StringComparison.Ordinal);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false));
        }
        catch (CryptographicException)
        {
            using var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            var parameters = ec.ExportParameters(includePrivateParameters: false);
            return new JsonWebKey
            {
                Kty = "EC",
                Crv = ReadCurveName(parameters.Curve),
                X = Base64UrlEncoder.Encode(parameters.Q.X),
                Y = Base64UrlEncoder.Encode(parameters.Q.Y)
            };
        }
    }

    private static string ReadCurveName(ECCurve curve)
        => curve.Oid.Value switch
        {
            "1.2.840.10045.3.1.7" => "P-256",
            "1.3.132.0.34" => "P-384",
            "1.3.132.0.35" => "P-521",
            _ => throw new CryptographicException("The PEM public key uses an unsupported EC curve.")
        };

    private static IReadOnlyCollection<string>? ParseAlgorithms(string? value)
    {
        var algorithms = value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (algorithms is not { Length: > 0 })
            return null;

        if (algorithms.Any(algorithm => string.Equals(algorithm, "none", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "AGENTUP_EXTERNAL_ALGORITHMS cannot include the unsecured 'none' algorithm.");
        }

        return algorithms;
    }

    private static IReadOnlyCollection<string> DefaultAlgorithms(SecurityKey? key, string? jwksUri)
        => key switch
        {
            SymmetricSecurityKey => [SecurityAlgorithms.HmacSha256],
            RsaSecurityKey => [SecurityAlgorithms.RsaSha256],
            JsonWebKey { Kty: "EC", Crv: "P-256" } => [SecurityAlgorithms.EcdsaSha256],
            JsonWebKey { Kty: "EC", Crv: "P-384" } => [SecurityAlgorithms.EcdsaSha384],
            JsonWebKey { Kty: "EC", Crv: "P-521" } => [SecurityAlgorithms.EcdsaSha512],
            _ when jwksUri is not null => [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256],
            _ => []
        };

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
