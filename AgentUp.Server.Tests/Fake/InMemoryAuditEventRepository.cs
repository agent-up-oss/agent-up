using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Tests.Fake;

internal sealed class InMemoryAuditEventRepository : IAuditEventRepository
{
    private readonly List<AuditEvent> _events = [];

    public IReadOnlyList<AuditEvent> Events => _events;

    public Task AppendAsync(AuditEvent evt, CancellationToken cancellationToken)
    {
        _events.Add(evt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken)
    {
        var result = _events
            .Where(evt => Matches(query.WorkspaceId, evt.WorkspaceId)
                          && Matches(query.WorkdirId, evt.WorkdirId)
                          && Matches(query.RepositoryPath, evt.RepositoryPath)
                          && Matches(query.Branch, evt.Branch)
                          && Matches(query.Commit, evt.Commit)
                          && Matches(query.Kind, evt.Kind)
                          && Matches(query.Source, evt.Source)
                          && Matches(query.Outcome, evt.Outcome)
                          && MatchesApplication(query.Application, evt)
                          && MatchesScope(query.Scope, evt.Scope)
                          && (!query.From.HasValue || evt.Timestamp >= query.From.Value)
                          && (!query.To.HasValue || evt.Timestamp <= query.To.Value)
                          && IsBeforeCursor(query, evt))
            .OrderByDescending(evt => evt.Timestamp)
            .ThenByDescending(evt => evt.EventId, StringComparer.Ordinal)
            .Take(Math.Clamp(query.Limit <= 0 ? 100 : query.Limit, 1, 500))
            .ToList();
        return Task.FromResult<IReadOnlyList<AuditEvent>>(result);
    }

    public Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken)
        => Task.FromResult(_events.FirstOrDefault(evt => evt.EventId == eventId));

    private static bool IsBeforeCursor(AuditEventQuery query, AuditEvent evt)
        => query.Before is null
           || evt.Timestamp < query.Before
           || (query.BeforeEventId is not null
               && evt.Timestamp == query.Before
               && string.CompareOrdinal(evt.EventId, query.BeforeEventId) < 0);

    private static bool MatchesApplication(string? application, AuditEvent evt)
        => string.IsNullOrWhiteSpace(application)
           || (evt.Details.TryGetValue("application", out var actual)
               && string.Equals(application, actual, StringComparison.Ordinal));

    private static bool MatchesScope(string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return true;

        var normalizedActual = string.IsNullOrWhiteSpace(actual) ? AuditScope.Workspace : actual;
        return string.Equals(expected, normalizedActual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(string? expected, string? actual)
        => string.IsNullOrWhiteSpace(expected)
           || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
