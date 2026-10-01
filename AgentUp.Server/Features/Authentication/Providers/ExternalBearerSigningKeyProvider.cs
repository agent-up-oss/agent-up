using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class ExternalBearerSigningKeyProvider
{
    private static readonly TimeSpan JwksCacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan JwksRefreshInterval = TimeSpan.FromMinutes(1);
    private readonly object _sync = new();
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly string? _jwksUri;
    private readonly SecurityKey? _configuredKey;
    private IReadOnlyCollection<SecurityKey>? _jwksKeys;
    private DateTimeOffset _jwksExpiresAt;
    private DateTimeOffset _nextJwksRefreshAt;

    public ExternalBearerSigningKeyProvider(
        IConfiguration configuration,
        HttpClient httpClient,
        TimeProvider? timeProvider = null)
    {
        ValidateConfiguration(configuration);
        _httpClient = httpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
        var sources = new[]
        {
            configuration["AGENTUP_EXTERNAL_SIGNING_KEY"],
            configuration["AGENTUP_EXTERNAL_PUBLIC_KEY"],
            configuration["AGENTUP_EXTERNAL_JWKS_URI"]
        }.Count(value => !string.IsNullOrWhiteSpace(value));

        if (sources > 1)
        {
            throw new InvalidOperationException(
                "Configure exactly one external bearer verification source: "
                + "AGENTUP_EXTERNAL_SIGNING_KEY, AGENTUP_EXTERNAL_PUBLIC_KEY, or AGENTUP_EXTERNAL_JWKS_URI.");
        }
    }

    public IEnumerable<SecurityKey> Resolve(string? keyId, bool refreshOnUnknownKey = false)
    {
        if (_configuredKey is not null)
            return [_configuredKey];
        if (_jwksUri is null)
            return [];

        var keys = GetJwksKeys(_jwksUri, forceRefresh: false);
        var matches = FindKeys(keys, keyId);
        if (matches.Count > 0 || !refreshOnUnknownKey)
            return matches;

        return FindKeys(GetJwksKeys(_jwksUri, forceRefresh: true), keyId);
    }

    private IReadOnlyCollection<SecurityKey> GetJwksKeys(string uri, bool forceRefresh)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            if (!forceRefresh && _jwksKeys is not null && now < _jwksExpiresAt)
                return _jwksKeys;
            if (now < _nextJwksRefreshAt && (forceRefresh || _jwksKeys is null))
                return _jwksKeys ?? [];

            _nextJwksRefreshAt = now.Add(JwksRefreshInterval);
            var json = _httpClient.GetStringAsync(uri).GetAwaiter().GetResult();
            _jwksKeys = new JsonWebKeySet(json).GetSigningKeys().ToArray();
            _jwksExpiresAt = now.Add(JwksCacheLifetime);
            return _jwksKeys;
        }
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
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return new RsaSecurityKey(rsa);
        }
        catch (CryptographicException)
        {
            var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            return new ECDsaSecurityKey(ec);
        }
    }

    private static IReadOnlyCollection<string>? ParseAlgorithms(string? value)
    {
        var algorithms = value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return algorithms is { Length: > 0 } ? algorithms : null;
    }

    private static IReadOnlyCollection<string> DefaultAlgorithms(SecurityKey? key, string? jwksUri)
        => key switch
        {
            SymmetricSecurityKey => [SecurityAlgorithms.HmacSha256],
            RsaSecurityKey => [SecurityAlgorithms.RsaSha256],
            ECDsaSecurityKey => [SecurityAlgorithms.EcdsaSha256],
            _ when jwksUri is not null => [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256],
            _ => []
        };

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
