using System.Text;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Audit.Providers;

internal static class AuditEventIndexEntryMatcher
{
    internal static bool Matches(AuditEventQuery query, AuditEventIndexEntry entry)
        => MatchesKind(query, entry.Kind)
           && MatchesStream(query, entry)
           && MatchesScope(query, entry.Scope)
           && IsBeforeCursor(query, entry);

    private static bool MatchesKind(AuditEventQuery query, string actual)
    {
        if (query.Kinds is { Count: > 0 })
            return query.Kinds.Any(expected => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(query.Kind)
               || string.Equals(query.Kind, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStream(AuditEventQuery query, AuditEventIndexEntry entry)
    {
        if (query.Streams is not { Count: > 0 })
            return true;

        if (!string.Equals(entry.Kind, "application", StringComparison.OrdinalIgnoreCase))
            return true;

        return entry.Stream is not null
               && query.Streams.Any(expected => string.Equals(expected, entry.Stream, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesScope(AuditEventQuery query, string? actual)
    {
        if (string.IsNullOrWhiteSpace(query.Scope))
            return true;

        var normalizedActual = string.IsNullOrWhiteSpace(actual) ? AuditScope.Workspace : actual;
        return string.Equals(query.Scope, normalizedActual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBeforeCursor(AuditEventQuery query, AuditEventIndexEntry entry)
        => query.Before is null
           || entry.Timestamp < query.Before
           || (query.BeforeEventId is not null
               && entry.Timestamp == query.Before
               && string.CompareOrdinal(entry.EventId, query.BeforeEventId) < 0);
}
