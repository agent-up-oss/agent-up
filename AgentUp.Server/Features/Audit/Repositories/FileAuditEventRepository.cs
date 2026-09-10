using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Providers;

namespace AgentUp.Server.Features.Audit.Repositories;

public sealed class FileAuditEventRepository : IAuditEventRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _dir;
    private readonly AuditApplicationIndexPathsProvider _indexPaths;
    private readonly SemaphoreSlim _appendLock = new(1, 1);
    private readonly Dictionary<string, SemaphoreSlim> _indexBuildLocks = new(StringComparer.Ordinal);

    public FileAuditEventRepository(string dataDir)
    {
        _dir = Path.GetFullPath(Path.Join(dataDir, "audit"));
        _indexPaths = new AuditApplicationIndexPathsProvider(_dir);
        Directory.CreateDirectory(_dir);
    }

    public async Task AppendAsync(AuditEvent evt, CancellationToken cancellationToken)
    {
        var file = DailyFile(evt.Timestamp);
        var json = JsonSerializer.Serialize(evt, Options);
        await _appendLock.WaitAsync(cancellationToken);
        try
        {
            var appended = await AuditEventOffsetLineReader.AppendLineAsync(file, json, cancellationToken);
            if (appended is null)
                return;

            if (string.IsNullOrWhiteSpace(evt.WorkspaceId)
                || !AuditApplicationNameResolver.TryGetApplication(evt, out var application))
                return;

            var indexFile = _indexPaths.GetDailyIndexFile(evt.WorkspaceId, application, DateOnly.FromDateTime(evt.Timestamp.UtcDateTime));
            Directory.CreateDirectory(Path.GetDirectoryName(indexFile)!);
            var indexLine = AuditEventIndexLineCodec.Format(evt, appended.Value.Offset, appended.Value.Length);
            await File.AppendAllTextAsync(indexFile, indexLine + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _appendLock.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventQuery query, CancellationToken cancellationToken)
    {
        var limit = query.Limit <= 0 ? 100 : Math.Min(query.Limit, 500);
        if (CanUseApplicationIndex(query))
            return await QueryViaApplicationIndexAsync(query, limit, cancellationToken);

        return await QueryViaGlobalScanAsync(query, limit, cancellationToken);
    }

    public async Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken)
    {
        foreach (var file in GetRelevantFilesReverse(null, null))
        {
            await foreach (var line in AuditEventLogReverseReader.ReadLinesReverseAsync(file, cancellationToken))
            {
                var evt = DeserializeLine(line);
                if (evt is not null && string.Equals(evt.EventId, eventId, StringComparison.Ordinal))
                    return evt;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<AuditEvent>> QueryViaApplicationIndexAsync(
        AuditEventQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var results = new List<AuditEvent>(limit);
        var upperBound = query.Before is null || (query.To is not null && query.To <= query.Before)
            ? query.To
            : query.Before;

        foreach (var date in GetRelevantDatesReverse(query.From, upperBound))
        {
            var indexFile = _indexPaths.GetDailyIndexFile(query.WorkspaceId!, query.Application!, date);
            await EnsureIndexBuiltAsync(query.WorkspaceId!, query.Application!, date, cancellationToken);
            if (!File.Exists(indexFile))
                continue;

            var eventFile = DailyFile(date);
            await CollectMatchesFromIndexFileAsync(eventFile, indexFile, query, results, limit, cancellationToken);
            if (results.Count >= limit)
                break;
        }

        return results;
    }

    private async Task<IReadOnlyList<AuditEvent>> QueryViaGlobalScanAsync(
        AuditEventQuery query,
        int limit,
        CancellationToken cancellationToken)
    {
        var upperBound = query.Before is null || (query.To is not null && query.To <= query.Before)
            ? query.To
            : query.Before;

        var results = new List<AuditEvent>(limit);
        foreach (var file in GetRelevantFilesReverse(query.From, upperBound))
        {
            await CollectMatchesFromFileReverseAsync(file, query, results, limit, cancellationToken);
            if (results.Count >= limit)
                break;
        }

        return results;
    }

    private async Task CollectMatchesFromIndexFileAsync(
        string eventFile,
        string indexFile,
        AuditEventQuery query,
        List<AuditEvent> results,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(eventFile))
            return;

        await foreach (var indexLine in AuditEventLogReverseReader.ReadLinesReverseAsync(indexFile, cancellationToken))
        {
            var parsed = AuditEventIndexLineCodec.TryParse(indexLine, out var entry);
            if (!parsed || !AuditEventIndexEntryMatcher.Matches(query, entry))
                continue;

            var eventLine = await AuditEventOffsetLineReader.ReadLineAsync(
                eventFile,
                entry.Offset,
                entry.Length,
                cancellationToken);
            if (eventLine is null)
                continue;

            var evt = DeserializeLine(eventLine, query);
            if (evt is null || !Matches(query, evt))
                continue;

            AddMatch(results, evt, limit);
            if (results.Count >= limit && CanStopScanningIndex(results, limit, entry))
                return;
        }
    }

    private async Task EnsureIndexBuiltAsync(
        string workspaceId,
        string application,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var indexFile = _indexPaths.GetDailyIndexFile(workspaceId, application, date);
        if (File.Exists(indexFile))
            return;

        var buildLock = GetIndexBuildLock(indexFile);
        await buildLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(indexFile))
                return;

            var eventFile = DailyFile(date);
            if (!File.Exists(eventFile))
            {
                await File.WriteAllTextAsync(indexFile, string.Empty, cancellationToken);
                return;
            }

            var query = new AuditEventQuery(
                workspaceId,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                Application: application);
            var indexLines = new List<string>();
            await foreach (var (offset, length, line) in AuditEventOffsetLineReader.ReadLinesForwardWithOffsetsAsync(
                               eventFile,
                               cancellationToken))
            {
                if (!AuditEventLogLinePrefilter.MightMatch(query, line))
                    continue;

                var evt = DeserializeLine(line, query);
                if (evt is null || !Matches(query, evt))
                    continue;

                indexLines.Add(AuditEventIndexLineCodec.Format(evt, offset, length));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(indexFile)!);
            await File.WriteAllTextAsync(
                indexFile,
                indexLines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, indexLines) + Environment.NewLine,
                cancellationToken);
        }
        finally
        {
            buildLock.Release();
        }
    }

    private SemaphoreSlim GetIndexBuildLock(string indexFile)
    {
        lock (_indexBuildLocks)
        {
            if (!_indexBuildLocks.TryGetValue(indexFile, out var buildLock))
            {
                buildLock = new SemaphoreSlim(1, 1);
                _indexBuildLocks[indexFile] = buildLock;
            }

            return buildLock;
        }
    }

    private static bool CanUseApplicationIndex(AuditEventQuery query)
        => !string.IsNullOrWhiteSpace(query.WorkspaceId)
           && !string.IsNullOrWhiteSpace(query.Application);

    private string DailyFile(DateTimeOffset date)
        => DailyFile(DateOnly.FromDateTime(date.UtcDateTime));

    private string DailyFile(DateOnly date)
        => Path.Join(_dir, $"events-{date:yyyy-MM-dd}.jsonl");

    private async Task CollectMatchesFromFileReverseAsync(
        string path,
        AuditEventQuery query,
        List<AuditEvent> results,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return;

        await foreach (var line in AuditEventLogReverseReader.ReadLinesReverseAsync(path, cancellationToken))
        {
            var evt = DeserializeLine(line, query);
            if (evt is null || !Matches(query, evt))
                continue;

            AddMatch(results, evt, limit);
            if (results.Count >= limit && CanStopScanningFile(results, limit, evt))
                return;
        }
    }

    private static AuditEvent? DeserializeLine(string line)
        => DeserializeLine(line, AllEventsQuery);

    private static AuditEvent? DeserializeLine(string line, AuditEventQuery query)
    {
        if (!AuditEventLogLinePrefilter.MightMatch(query, line))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AuditEvent>(line, Options);
        }
        catch (JsonException ex)
        {
            Trace.TraceWarning($"[FileAuditEventRepository] Skipped malformed audit event line: {ex.Message}");
            return null;
        }
    }

    private static readonly AuditEventQuery AllEventsQuery = new(
        null, null, null, null, null, null, null, null, null, null, 1);

    private static void AddMatch(List<AuditEvent> results, AuditEvent candidate, int limit)
    {
        var insertAt = 0;
        while (insertAt < results.Count && CompareDescending(candidate, results[insertAt]) <= 0)
            insertAt++;

        if (insertAt >= limit)
            return;

        results.Insert(insertAt, candidate);
        if (results.Count > limit)
            results.RemoveAt(results.Count - 1);
    }

    private static bool CanStopScanningFile(IReadOnlyList<AuditEvent> results, int limit, AuditEvent candidate)
        => results.Count >= limit && CompareDescending(candidate, results[^1]) < 0;

    private static bool CanStopScanningIndex(IReadOnlyList<AuditEvent> results, int limit, AuditEventIndexEntry entry)
        => results.Count >= limit
           && CompareDescending(entry.Timestamp, entry.EventId, results[^1].Timestamp, results[^1].EventId) < 0;

    private IEnumerable<string> GetRelevantFilesReverse(DateTimeOffset? from, DateTimeOffset? to)
    {
        foreach (var date in GetRelevantDatesReverse(from, to))
            yield return DailyFile(date);

        var legacy = Path.Join(_dir, "events.jsonl");
        if (File.Exists(legacy))
            yield return legacy;
    }

    private IEnumerable<DateOnly> GetRelevantDatesReverse(DateTimeOffset? from, DateTimeOffset? to)
    {
        var fromDate = from.HasValue ? DateOnly.FromDateTime(from.Value.UtcDateTime) : (DateOnly?)null;
        var toDate = to.HasValue ? DateOnly.FromDateTime(to.Value.UtcDateTime) : (DateOnly?)null;

        var dated = Directory.GetFiles(_dir, "events-????-??-??.jsonl")
            .Select(file => (parsed: TryGetFileDate(file, out var date), date))
            .Where(entry => entry.parsed)
            .Where(entry => !fromDate.HasValue || entry.date >= fromDate.Value)
            .Where(entry => !toDate.HasValue || entry.date <= toDate.Value)
            .OrderByDescending(entry => entry.date)
            .Select(entry => entry.date);
        foreach (var date in dated)
            yield return date;
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

    private static int CompareDescending(AuditEvent left, AuditEvent right)
        => CompareDescending(left.Timestamp, left.EventId, right.Timestamp, right.EventId);

    private static int CompareDescending(
        DateTimeOffset leftTimestamp,
        string leftEventId,
        DateTimeOffset rightTimestamp,
        string rightEventId)
    {
        var timestamp = leftTimestamp.CompareTo(rightTimestamp);
        return timestamp != 0
            ? timestamp
            : string.CompareOrdinal(leftEventId, rightEventId);
    }

    private static bool Matches(AuditEventQuery query, AuditEvent evt)
        => Matches(query.WorkspaceId, evt.WorkspaceId)
           && Matches(query.WorkdirId, evt.WorkdirId)
           && Matches(query.RepositoryPath, evt.RepositoryPath)
           && Matches(query.Branch, evt.Branch)
           && Matches(query.Commit, evt.Commit)
           && MatchesKind(query.Kind, query.Kinds, evt.Kind)
           && MatchesStream(query.Streams, evt)
           && Matches(query.Source, evt.Source)
           && Matches(query.Outcome, evt.Outcome)
           && MatchesApplication(query.Application, evt)
           && MatchesScope(query.Scope, evt.Scope)
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
        => AuditEventMatching.MatchesApplication(application, evt);

    private static bool MatchesKind(string? kind, IReadOnlyList<string>? kinds, string? actual)
        => AuditEventMatching.MatchesKind(kind, kinds, actual);

    private static bool MatchesStream(IReadOnlyList<string>? streams, AuditEvent evt)
        => AuditEventMatching.MatchesStreams(streams, evt);

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
