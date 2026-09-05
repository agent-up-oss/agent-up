using AgentUp.Desktop.Features.Metrics.Services;

namespace AgentUp.Desktop.Features.Metrics.Controllers;

public sealed class HostMetricsController : IDisposable
{
    private readonly HostMetricsReporter _reporter;

    public HostMetricsController(HttpClient http) => _reporter = new HostMetricsReporter(http);

    public void Start() => _reporter.Start();

    public void Stop() => _reporter.Stop();

    public void Dispose() => _reporter.Dispose();
}
