using AgentUp.Desktop.Features.Metrics.Services;

namespace AgentUp.Desktop.Features.Metrics.Controllers;

public sealed class HostMetricsController(HostMetricsReporter reporter) : IDisposable
{
    public void Start() => reporter.Start();

    public void Stop() => reporter.Stop();

    public void Dispose() => reporter.Dispose();
}
