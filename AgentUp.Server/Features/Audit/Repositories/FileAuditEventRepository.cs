using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Audit.Repositories;

public sealed class FileAuditEventRepository : IAuditEventRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileAuditEventRepository(string dataDir)
    {
        _path = Path.GetFullPath(Path.Join(dataDir, "audit", "events.jsonl"));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public async Task AppendAsync(AuditEvent evt, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(evt, Options);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_path, json + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken)
    {
        var events = await LoadAsync(cancellationToken);
        return events
            .Where(evt => Matches(query, evt))
            .OrderByDescending(evt => evt.Timestamp)
            .Take(query.Limit <= 0 ? 100 : Math.Min(query.Limit, 500))
            .ToList();
    }

    public async Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken)
        => (await LoadAsync(cancellationToken))
            .FirstOrDefault(evt => string.Equals(evt.EventId, eventId, StringComparison.Ordinal));

    private async Task<IReadOnlyList<AuditEvent>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return [];

        var events = new List<AuditEvent>();
        await foreach (var line in File.ReadLinesAsync(_path, cancellationToken)
                           .Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            try
            {
                var evt = JsonSerializer.Deserialize<AuditEvent>(line, Options);
                if (evt is not null)
                    events.Add(evt);
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return events;
    }

    private static bool Matches(AuditEventQuery query, AuditEvent evt)
        => Matches(query.WorkspaceId, evt.WorkspaceId)
           && Matches(query.WorkdirId, evt.WorkdirId)
           && Matches(query.RepositoryPath, evt.RepositoryPath)
           && Matches(query.Branch, evt.Branch)
           && Matches(query.Commit, evt.Commit)
           && Matches(query.Kind, evt.Kind)
           && Matches(query.Source, evt.Source)
           && Matches(query.Outcome, evt.Outcome)
           && (query.From is null || evt.Timestamp >= query.From)
           && (query.To is null || evt.Timestamp <= query.To);

    private static bool Matches(string? expected, string? actual)
        => string.IsNullOrWhiteSpace(expected)
           || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
