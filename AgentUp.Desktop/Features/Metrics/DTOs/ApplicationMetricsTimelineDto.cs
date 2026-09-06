namespace AgentUp.Desktop.Features.Metrics.DTOs;

public sealed record ApplicationMetricsTimelineDto(
    IReadOnlyList<MetricsSummaryCardDto> Summary,
    IReadOnlyList<MetricsSeriesDto> Series);

public sealed record MetricsSummaryCardDto(string Label, string Value, string? Hint);

public sealed record MetricsSeriesDto(
    string Key,
    string Title,
    string Unit,
    IReadOnlyList<MetricsPointDto> Points);

public sealed record MetricsPointDto(DateTimeOffset Timestamp, double Value);
