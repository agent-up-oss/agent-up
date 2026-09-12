using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AgentUp.Server.Features.DesktopApplications.Providers;

public sealed class DesktopViewerTicketProvider
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);
    private readonly ConcurrentDictionary<string, DesktopViewerTicket> _tickets = new();

    public (string Ticket, DateTimeOffset ExpiresAtUtc) Issue(string sessionId)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(Lifetime);
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _tickets[ticket] = new DesktopViewerTicket(sessionId, expiresAt);
        RemoveExpired();
        return (ticket, expiresAt);
    }

    public bool Validate(string sessionId, string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || !_tickets.TryGetValue(ticket, out var issued))
            return false;
        return issued.ExpiresAtUtc >= DateTimeOffset.UtcNow
               && CryptographicOperations.FixedTimeEquals(
                   System.Text.Encoding.UTF8.GetBytes(issued.SessionId),
                   System.Text.Encoding.UTF8.GetBytes(sessionId));
    }

    public void RevokeSession(string sessionId)
    {
        foreach (var entry in _tickets.Where(entry => entry.Value.SessionId == sessionId))
            _tickets.TryRemove(entry.Key, out _);
    }

    private void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _tickets.Where(entry => entry.Value.ExpiresAtUtc < now))
            _tickets.TryRemove(entry.Key, out _);
    }
}

public sealed record DesktopViewerTicket(string SessionId, DateTimeOffset ExpiresAtUtc);
