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
                          && Matches(query.Outcome, evt.Outcome))
            .OrderByDescending(evt => evt.Timestamp)
            .Take(query.Limit <= 0 ? 100 : query.Limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<AuditEvent>>(result);
    }

    public Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken)
        => Task.FromResult(_events.FirstOrDefault(evt => evt.EventId == eventId));

    private static bool Matches(string? expected, string? actual)
        => string.IsNullOrWhiteSpace(expected)
           || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
