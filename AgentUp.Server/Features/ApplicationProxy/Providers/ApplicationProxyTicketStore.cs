using System.Collections.Concurrent;
using System.Security.Cryptography;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyTicketStore : IApplicationProxyTicketStore
{
    private readonly ConcurrentDictionary<string, ApplicationProxySession> _tickets = new(StringComparer.Ordinal);

    public string Issue(ApplicationProxySession session)
    {
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _tickets[ticket] = session;
        return ticket;
    }

    public ApplicationProxySession? Consume(string ticket, DateTimeOffset now)
    {
        if (!_tickets.TryRemove(ticket, out var session))
            return null;

        return session.ExpiresAt > now ? session : null;
    }
}
