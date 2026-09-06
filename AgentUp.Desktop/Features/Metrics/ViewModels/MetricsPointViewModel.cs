using ReactiveUI;

namespace AgentUp.Desktop.Features.Metrics.ViewModels;

public sealed class MetricsPointViewModel : ReactiveObject
{
    public MetricsPointViewModel(DateTimeOffset timestamp, double value)
    {
        Timestamp = timestamp;
        Value = value;
    }

    public DateTimeOffset Timestamp { get; }
    public double Value { get; }
}
