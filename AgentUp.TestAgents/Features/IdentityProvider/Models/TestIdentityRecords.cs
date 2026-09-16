namespace AgentUp.TestAgents.Features.IdentityProvider.Models;

/// <summary>An authorization request parked until the user approves it in the browser.</summary>
/// <param name="ClientId">Which test agent asked.</param>
/// <param name="RedirectUri">Where to send the browser back to, for the loopback shape.</param>
/// <param name="State">CSRF state to echo back.</param>
/// <param name="CodeChallenge">PKCE S256 challenge, verified at the token endpoint.</param>
public sealed record PendingAuthorization(
    string ClientId,
    string? RedirectUri,
    string? State,
    string? CodeChallenge);

/// <summary>
/// A device authorization: the agent holds the device code and polls with it, while the user
/// carries the short user code into the browser.
/// </summary>
/// <param name="UserCode">The short code shown to the user.</param>
/// <param name="ClientId">Which test agent asked.</param>
/// <param name="ExpiresAt">When the grant stops being usable.</param>
/// <param name="Approved">Set once the user has approved it.</param>
public sealed record DeviceGrant(
    string UserCode,
    string ClientId,
    DateTimeOffset ExpiresAt,
    bool Approved)
{
    public DeviceGrant Approve() => this with { Approved = true };
}

/// <summary>
/// A sign-in the agent polls for without ever showing the user a code, keyed by an opaque id
/// carried in the deep link.
/// </summary>
public sealed record PollGrant(string ClientId, DateTimeOffset ExpiresAt, bool Approved)
{
    public PollGrant Approve() => this with { Approved = true };
}

/// <summary>An access token the identity provider issued.</summary>
public sealed record IssuedToken(string Value, string ClientId, DateTimeOffset IssuedAt);
