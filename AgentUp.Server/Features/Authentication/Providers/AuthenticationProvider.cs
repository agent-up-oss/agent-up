using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class AuthenticationProvider
{
    private readonly byte[]? _password;
    private readonly HashSet<string> _sessions = [];
    private readonly object _gate = new();

    public AuthenticationProvider(IConfiguration configuration)
    {
        var explicitlyDisabled = string.Equals(
            configuration["AGENTUP_AUTH_DISABLED"], "true", StringComparison.OrdinalIgnoreCase);
        var password = configuration["AGENTUP_ADMIN_PASSWORD"];

        if (explicitlyDisabled || string.IsNullOrEmpty(password))
        {
            IsRequired = false;
            _password = null;
            return;
        }

        IsRequired = true;
        _password = Encoding.UTF8.GetBytes(password);
    }

    public bool IsRequired { get; }

    public string? Login(string password)
    {
        if (!IsRequired) return null;
        var candidate = Encoding.UTF8.GetBytes(password ?? string.Empty);
        if (candidate.Length != _password!.Length || !CryptographicOperations.FixedTimeEquals(candidate, _password))
            return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        lock (_gate) _sessions.Add(token);
        return token;
    }

    public bool IsAuthenticated(string? token)
    {
        if (!IsRequired) return true;
        if (string.IsNullOrWhiteSpace(token)) return false;
        lock (_gate) return _sessions.Contains(token);
    }
}
