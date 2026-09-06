using System.Globalization;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Models;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Applications.Services;

public sealed class ApplicationMetricsService(AuditController audit)
{
    private static readonly (string[] Keys, string Label, string Unit)[] SummaryPriority =
    [
        (["latency_ms", "latency", "p95_ms", "duration_ms", "response_time_ms"], "Latency", "ms"),
        (["requests_per_minute", "req_per_min", "rpm", "requests_per_min", "throughput_rpm"], "Req/min", ""),
        (["errors_total", "error_count", "errors", "failed_requests"], "Errors", ""),
        (["success_rate", "success_ratio"], "Success rate", "%"),
        (["uptime_percent", "uptime", "availability"], "Uptime", "%"),
    ];

    public async Task<ApplicationMetricsTimelineDto> GetTimelineAsync(
        string workspaceId,
        string appName,
        int limit,
        CancellationToken cancellationToken)
    {
        var cappedLimit = Math.Clamp(limit, 1, 240);
        var events = await audit.QueryAsync(
            new AuditEventQuery(
                workspaceId,
                null,
                null,
                null,
                null,
                "metrics",
                null,
                null,
                null,
                null,
                cappedLimit,
                AuditScope.Application,
                Application: appName),
            cancellationToken);

        var samples = events
            .Where(evt => string.Equals(evt.Action, "app_metrics_pull", StringComparison.Ordinal)
                          && string.Equals(evt.Details.GetValueOrDefault("application"), appName, StringComparison.Ordinal))
            .OrderBy(evt => evt.Timestamp)
            .Select(evt => new MetricsSample(evt.Timestamp, ExtractMetricValues(evt.Details)))
            .Where(sample => sample.Values.Count > 0)
            .ToList();

        var series = BuildSeries(samples);
        var summary = BuildSummary(samples, series);
        return new ApplicationMetricsTimelineDto(summary, series);
    }

    private static IReadOnlyDictionary<string, double> ExtractMetricValues(
        IReadOnlyDictionary<string, string> details)
        => details
            .Where(pair => pair.Key.StartsWith("metric.", StringComparison.Ordinal))
            .Select(pair => new KeyValuePair<string, string>(pair.Key["metric.".Length..], pair.Value))
            .Select(pair => TryParseMetricValue(pair.Value, out var value)
                ? new KeyValuePair<string, double>?(new KeyValuePair<string, double>(pair.Key, value))
                : null)
            .Where(pair => pair.HasValue)
            .Select(pair => pair!.Value)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static bool TryParseMetricValue(string raw, out double value)
    {
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        if (raw.EndsWith('%') && double.TryParse(raw[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        value = 0;
        return false;
    }

    private static IReadOnlyList<MetricsSeriesDto> BuildSeries(IReadOnlyList<MetricsSample> samples)
    {
        var keys = samples
            .SelectMany(sample => sample.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => ChartPriority(key))
            .ThenBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return keys
            .Select(key => new MetricsSeriesDto(
                key,
                FormatSeriesTitle(key),
                InferUnit(key),
                samples
                    .Where(sample => sample.Values.ContainsKey(key))
                    .Select(sample => new MetricsPointDto(sample.Timestamp, sample.Values[key]))
                    .ToList()))
            .Where(series => series.Points.Count > 0)
            .ToList();
    }

    private static IReadOnlyList<MetricsSummaryCardDto> BuildSummary(
        IReadOnlyList<MetricsSample> samples,
        IReadOnlyList<MetricsSeriesDto> series)
    {
        if (samples.Count == 0)
            return [];

        var latest = samples[^1].Values;
        var cards = new List<MetricsSummaryCardDto>();

        foreach (var (keys, label, unit) in SummaryPriority)
        {
            var match = keys
                .Select(key => FindMetric(latest, key))
                .FirstOrDefault(result => result.Found);
            if (!match.Found)
                continue;

            cards.Add(new MetricsSummaryCardDto(label, FormatSummaryValue(match.Value, unit), unit));
            if (cards.Count == 4)
                return cards;
        }

        foreach (var extra in series.Take(4 - cards.Count))
        {
            var value = extra.Points[^1].Value;
            cards.Add(new MetricsSummaryCardDto(extra.Title, FormatSummaryValue(value, extra.Unit), extra.Unit));
        }

        return cards;
    }

    private static (bool Found, double Value) FindMetric(IReadOnlyDictionary<string, double> values, string key)
    {
        var match = values.FirstOrDefault(pair =>
            string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
            || pair.Key.EndsWith(key, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrEmpty(match.Key) ? (false, 0) : (true, match.Value);
    }

    private static int ChartPriority(string key)
    {
        var lower = key.ToLowerInvariant();
        if (lower.Contains("latency") || lower.Contains("duration") || lower.Contains("response")) return 0;
        if (lower.Contains("request") || lower.Contains("throughput") || lower.Contains("rpm")) return 1;
        if (lower.Contains("error") || lower.Contains("fail") || lower.Contains("success")) return 2;
        if (lower.Contains("cpu")) return 3;
        if (lower.Contains("memory") || lower.Contains("heap") || lower.Contains("bytes")) return 4;
        if (lower.Contains("connection") || lower.Contains("active")) return 5;
        return 10;
    }

    private static string FormatSeriesTitle(string key)
        => key.Replace('_', ' ').Replace('.', ' ');

    private static string InferUnit(string key)
    {
        var lower = key.ToLowerInvariant();
        if (lower.Contains("percent") || lower.EndsWith("_pct") || lower.Contains("rate") || lower.Contains("ratio") || lower == "uptime") return "%";
        if (lower.Contains("ms") || lower.Contains("latency") || lower.Contains("duration")) return "ms";
        if (lower.Contains("byte") || lower.Contains("memory") || lower.Contains("heap")) return "bytes";
        if (lower.Contains("request") || lower.Contains("rpm") || lower.Contains("throughput")) return "/min";
        return string.Empty;
    }

    private static string FormatSummaryValue(double value, string unit)
    {
        if (unit == "%")
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "%";

        if (unit == "ms")
        {
            if (value <= 0)
                return "—";
            return value >= 1000
                ? (value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "s"
                : value.ToString("0", CultureInfo.InvariantCulture) + "ms";
        }

        if (unit == "bytes")
            return value >= 1_048_576
                ? (value / 1_048_576).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
                : value >= 1024
                    ? (value / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " KB"
                    : value.ToString("0", CultureInfo.InvariantCulture) + " B";

        if (value >= 1_000_000)
            return (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M";

        if (value >= 1_000)
            return (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k";

        return value.ToString("0.##", CultureInfo.InvariantCulture) + (unit == "/min" ? "/min" : string.Empty);
    }
}
