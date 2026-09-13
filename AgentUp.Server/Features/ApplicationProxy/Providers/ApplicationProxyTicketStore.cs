using System.Collections.Concurrent;
using System.Security.Cryptography;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyTicketStore : IApplicationProxyTicketStore
{
    private readonly ConcurrentDictionary<string, ApplicationProxySession> _tickets = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    public ApplicationProxyTicketStore()
        : this(TimeProvider.System)
    {
    }

    public ApplicationProxyTicketStore(TimeProvider clock)
        => _clock = clock;

    public string Issue(ApplicationProxySession session)
    {
        EvictExpired(_clock.GetUtcNow());
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _tickets[ticket] = session;
        return ticket;
    }

    public ApplicationProxySession? Consume(string ticket, DateTimeOffset now)
    {
        EvictExpired(now);
        if (!_tickets.TryRemove(ticket, out var session))
            return null;

        return session.ExpiresAt > now ? session : null;
    }

    private void EvictExpired(DateTimeOffset now)
    {
        foreach (var pair in _tickets.Where(entry => entry.Value.ExpiresAt <= now))
            _tickets.TryRemove(pair.Key, out _);
    }
}
