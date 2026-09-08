using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class AuthenticationProvider
{
    private readonly byte[]? _password;
    private readonly TimeSpan _sessionLifetime;
    private readonly Dictionary<string, DateTimeOffset> _sessions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public AuthenticationProvider(IConfiguration configuration)
    {
        var explicitlyDisabled = string.Equals(
            configuration["AGENTUP_AUTH_DISABLED"], "true", StringComparison.OrdinalIgnoreCase);
        var password = configuration["AGENTUP_ADMIN_PASSWORD"];

        IsRequired = !explicitlyDisabled;
        _password = string.IsNullOrEmpty(password) ? null : Encoding.UTF8.GetBytes(password);
        _sessionLifetime = TimeSpan.FromSeconds(configuration.GetValue("AGENTUP_SESSION_LIFETIME_SECONDS", 86_400));
    }

    public bool IsRequired { get; }

    public string? Login(string password)
    {
        if (!IsRequired || _password is null) return null;
        var candidate = Encoding.UTF8.GetBytes(password ?? string.Empty);
        if (candidate.Length != _password!.Length || !CryptographicOperations.FixedTimeEquals(candidate, _password))
            return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        lock (_gate) _sessions[token] = DateTimeOffset.UtcNow.Add(_sessionLifetime);
        return token;
    }

    public bool IsAuthenticated(string? token)
    {
        if (!IsRequired) return true;
        if (string.IsNullOrWhiteSpace(token)) return false;

        lock (_gate)
        {
            if (!_sessions.TryGetValue(token, out var expiresAt))
                return false;

            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                _sessions.Remove(token);
                return false;
            }

            return true;
        }
    }
}
