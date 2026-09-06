using System.Collections.ObjectModel;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Metrics.ViewModels;

public sealed class MetricsChartViewModel : ReactiveObject
{
    public MetricsChartViewModel(
        string title,
        string unit,
        string latestDisplay,
        IReadOnlyList<MetricsPointViewModel> points)
    {
        Title = title;
        Unit = unit;
        LatestDisplay = latestDisplay;
        Points = new ObservableCollection<MetricsPointViewModel>(points);
    }

    public string Title { get; }
    public string Unit { get; }
    public string LatestDisplay { get; }
    public ObservableCollection<MetricsPointViewModel> Points { get; }
}
