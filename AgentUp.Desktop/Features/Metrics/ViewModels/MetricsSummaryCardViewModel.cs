using ReactiveUI;

namespace AgentUp.Desktop.Features.Metrics.ViewModels;

public sealed class MetricsSummaryCardViewModel : ReactiveObject
{
    public MetricsSummaryCardViewModel(string label, string value, string? hint = null)
    {
        Label = label;
        Value = value;
        Hint = hint;
    }

    public string Label { get; }
    public string Value { get; }
    public string? Hint { get; }
}
