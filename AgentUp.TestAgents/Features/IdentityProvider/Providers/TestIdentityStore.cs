using System.Collections.Concurrent;
using AgentUp.TestAgents.Features.IdentityProvider.Models;
using AgentUp.TestAgents.Shared.Providers;

namespace AgentUp.TestAgents.Features.IdentityProvider.Providers;

/// <summary>
/// The identity provider's state. Everything is keyed by a secret the caller already holds, so a
/// test can approve a specific sign-in without racing any other sign-in running beside it.
/// </summary>
public sealed class TestIdentityStore
{
    private readonly ConcurrentDictionary<string, PendingAuthorization> _authorizations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DeviceGrant> _devices = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PollGrant> _logins = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IssuedToken> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _preApproved = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _latestCodes = new(StringComparer.Ordinal);

    /// <summary>
    /// Clients a test approved up front. Their authorization requests redirect straight back
    /// instead of rendering a consent page, which is what lets an end-to-end test exercise a real
    /// redirect without driving a browser's DOM.
    /// </summary>
    public bool IsPreApproved(string clientId) => _preApproved.ContainsKey(clientId);

    public void PreApprove(string clientId) => _preApproved[clientId] = true;

    public string StartAuthorization(PendingAuthorization authorization)
    {
        var code = PkceVerifier.Secret(24);
        _authorizations[code] = authorization;
        _latestCodes[authorization.ClientId] = code;
        return code;
    }

    /// <summary>The code most recently issued to a client, for a test that has to paste it back.</summary>
    public string? LatestCode(string clientId) => _latestCodes.GetValueOrDefault(clientId);

    public PendingAuthorization? RedeemAuthorization(string code)
    {
        // One-time: a replayed code has to fail the way a real provider makes it fail.
        return _authorizations.TryRemove(code, out var authorization) ? authorization : null;
    }

    public (string DeviceCode, DeviceGrant Grant) StartDevice(string clientId, TimeSpan lifetime)
    {
        var deviceCode = PkceVerifier.Secret(24);
        var grant = new DeviceGrant(PkceVerifier.UserCode(), clientId, DateTimeOffset.UtcNow + lifetime, false);
        _devices[deviceCode] = grant;
        _latestCodes[clientId] = deviceCode;
        return (deviceCode, grant);
    }

    public string? DeviceCodeFor(string userCode) =>
        _devices.FirstOrDefault(entry => string.Equals(entry.Value.UserCode, userCode, StringComparison.OrdinalIgnoreCase)).Key;

    public DeviceGrant? Device(string deviceCode) => _devices.GetValueOrDefault(deviceCode);

    public bool ApproveDevice(string deviceCode)
    {
        if (!_devices.TryGetValue(deviceCode, out var grant))
            return false;
        _devices[deviceCode] = grant.Approve();
        return true;
    }

    public string StartLogin(string clientId, TimeSpan lifetime)
    {
        var loginId = PkceVerifier.Secret(16);
        _logins[loginId] = new PollGrant(clientId, DateTimeOffset.UtcNow + lifetime, false);
        return loginId;
    }

    public PollGrant? Login(string loginId) => _logins.GetValueOrDefault(loginId);

    public bool ApproveLogin(string loginId)
    {
        if (!_logins.TryGetValue(loginId, out var grant))
            return false;
        _logins[loginId] = grant.Approve();
        return true;
    }

    public IssuedToken Issue(string clientId)
    {
        var token = new IssuedToken($"test-oat-{PkceVerifier.Secret(24)}", clientId, DateTimeOffset.UtcNow);
        _tokens[token.Value] = token;
        return token;
    }

    public bool IsValid(string token) => _tokens.ContainsKey(token);

    public void Reset()
    {
        _authorizations.Clear();
        _devices.Clear();
        _logins.Clear();
        _tokens.Clear();
        _preApproved.Clear();
        _latestCodes.Clear();
    }
}
