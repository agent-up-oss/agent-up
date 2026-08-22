using System.Diagnostics;
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

    private readonly string _dir;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileAuditEventRepository(string dataDir)
    {
        _dir = Path.GetFullPath(Path.Join(dataDir, "audit"));
        Directory.CreateDirectory(_dir);
    }

    public async Task AppendAsync(AuditEvent evt, CancellationToken cancellationToken)
    {
        var file = DailyFile(DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(evt, Options);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(file, json + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken)
    {
        var upperBound = query.Before is null || (query.To is not null && query.To <= query.Before)
            ? query.To
            : query.Before;
        var events = await LoadRangeAsync(query.From, upperBound, cancellationToken);
        return events
            .Where(evt => Matches(query, evt))
            .OrderByDescending(evt => evt.Timestamp)
            .ThenByDescending(evt => evt.EventId, StringComparer.Ordinal)
            .Take(query.Limit <= 0 ? 100 : Math.Min(query.Limit, 500))
            .ToList();
    }

    public async Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken)
        => (await LoadRangeAsync(null, null, cancellationToken))
            .FirstOrDefault(evt => string.Equals(evt.EventId, eventId, StringComparison.Ordinal));

    private string DailyFile(DateTimeOffset date)
        => Path.Join(_dir, $"events-{date.UtcDateTime:yyyy-MM-dd}.jsonl");

    private async Task<List<AuditEvent>> LoadRangeAsync(
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var events = new List<AuditEvent>();
            foreach (var file in GetRelevantFiles(from, to))
                await AppendFromFileAsync(file, events, cancellationToken);
            return events;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IEnumerable<string> GetRelevantFiles(DateTimeOffset? from, DateTimeOffset? to)
    {
        // Backward compat: monolithic legacy file
        var legacy = Path.Join(_dir, "events.jsonl");
        if (File.Exists(legacy))
            yield return legacy;

        var fromDate = from.HasValue ? DateOnly.FromDateTime(from.Value.UtcDateTime) : (DateOnly?)null;
        var toDate = to.HasValue ? DateOnly.FromDateTime(to.Value.UtcDateTime) : (DateOnly?)null;

        var dated = Directory.GetFiles(_dir, "events-????-??-??.jsonl")
            .Order()
            .Select(file => (file, parsed: TryGetFileDate(file, out var d), date: d))
            .Where(x => x.parsed)
            .Where(x => !fromDate.HasValue || x.date >= fromDate.Value)
            .Where(x => !toDate.HasValue || x.date <= toDate.Value);
        foreach (var (file, _, _) in dated)
            yield return file;
    }

    private static bool TryGetFileDate(string path, out DateOnly date)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        const string prefix = "events-";
        if (!stem.StartsWith(prefix, StringComparison.Ordinal))
        {
            date = default;
            return false;
        }
        return DateOnly.TryParseExact(stem[prefix.Length..], "yyyy-MM-dd", out date);
    }

    private static async Task AppendFromFileAsync(string path, List<AuditEvent> events, CancellationToken ct)
    {
        if (!File.Exists(path)) return;
        await foreach (var line in File.ReadLinesAsync(path, ct)
                           .Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            try
            {
                var evt = JsonSerializer.Deserialize<AuditEvent>(line, Options);
                if (evt is not null)
                    events.Add(evt);
            }
            catch (JsonException ex)
            {
                Trace.TraceWarning($"[FileAuditEventRepository] Skipped malformed audit event line: {ex.Message}");
            }
        }
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
           && MatchesApplication(query.Application, evt)
           && (query.From is null || evt.Timestamp >= query.From)
           && (query.To is null || evt.Timestamp <= query.To)
           && IsBeforeCursor(query, evt);

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

    private static bool Matches(string? expected, string? actual)
        => string.IsNullOrWhiteSpace(expected)
           || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
