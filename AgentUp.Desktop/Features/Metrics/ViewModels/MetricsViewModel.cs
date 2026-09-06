using System.Collections.ObjectModel;
using System.Globalization;
using AgentUp.Desktop.Features.Metrics.Controllers;
using AgentUp.Desktop.Features.Metrics.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Metrics.ViewModels;

public sealed class MetricsViewModel : ReactiveObject
{
    private readonly MetricsController _metrics;
    private bool _isLoading;
    private string? _emptyMessage;
    private string? _lastUpdated;

    public ObservableCollection<MetricsSummaryCardViewModel> SummaryCards { get; } = [];
    public ObservableCollection<MetricsChartViewModel> Charts { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool HasSummary => SummaryCards.Count > 0;

    public bool HasCharts => Charts.Count > 0;

    public bool HasData => HasSummary || HasCharts;

    public bool ShowEmptyState => !IsLoading && !HasData;

    public string? EmptyMessage
    {
        get => _emptyMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _emptyMessage, value);
            this.RaisePropertyChanged(nameof(HasData));
        }
    }

    public string? LastUpdated
    {
        get => _lastUpdated;
        private set => this.RaiseAndSetIfChanged(ref _lastUpdated, value);
    }

    public MetricsViewModel(MetricsController metrics) => _metrics = metrics;

    public void Clear()
    {
        SummaryCards.Clear();
        Charts.Clear();
        EmptyMessage = null;
        LastUpdated = null;
        RaiseDataProperties();
    }

    public async Task LoadAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var timeline = await _metrics.GetTimelineAsync(workspaceId, appName, ct);
            ApplyTimeline(timeline);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            EmptyMessage = "Could not load metrics from the Server.";
            SummaryCards.Clear();
            Charts.Clear();
            RaiseDataProperties();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyTimeline(ApplicationMetricsTimelineDto? timeline)
    {
        SummaryCards.Clear();
        Charts.Clear();

        if (timeline is null || (timeline.Summary.Count == 0 && timeline.Series.Count == 0))
        {
            EmptyMessage = "No metrics yet. Add a metrics path to a port in agent-up.json, for example \"metrics\": \"/metrics\".";
            RaiseDataProperties();
            return;
        }

        EmptyMessage = null;
        foreach (var card in timeline.Summary)
            SummaryCards.Add(new MetricsSummaryCardViewModel(card.Label, card.Value, card.Hint));

        foreach (var series in timeline.Series)
        {
            var points = series.Points
                .Select(point => new MetricsPointViewModel(point.Timestamp, point.Value))
                .ToList();
            if (points.Count == 0 || points.All(point => point.Value == 0))
                continue;

            var latest = points[^1].Value;
            Charts.Add(new MetricsChartViewModel(
                series.Title,
                series.Unit,
                FormatLatest(latest, series.Unit),
                points));
        }

        var latestTimestamp = timeline.Series
            .SelectMany(series => series.Points)
            .Select(point => point.Timestamp)
            .DefaultIfEmpty()
            .Max();
        LastUpdated = latestTimestamp == default
            ? null
            : $"Updated {latestTimestamp.ToLocalTime():HH:mm:ss}";
        RaiseDataProperties();
    }

    private void RaiseDataProperties()
    {
        this.RaisePropertyChanged(nameof(HasSummary));
        this.RaisePropertyChanged(nameof(HasCharts));
        this.RaisePropertyChanged(nameof(HasData));
        this.RaisePropertyChanged(nameof(ShowEmptyState));
    }

    private static string FormatLatest(double value, string unit)
    {
        if (unit == "%")
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        if (unit == "ms")
            return value <= 0
                ? "—"
                : value.ToString("0", CultureInfo.InvariantCulture) + " ms";
        if (unit == "bytes")
            return value >= 1_048_576
                ? (value / 1_048_576).ToString("0.#", CultureInfo.InvariantCulture) + " MB"
                : value.ToString("0", CultureInfo.InvariantCulture) + " B";
        if (value >= 1_000)
            return (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k";
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
