using AgentUp.Desktop.Features.Metrics.Services;

namespace AgentUp.Desktop.Tests.Features.Metrics.Unit;

[TestFixture]
public sealed class HostMetricsReporterTests
{
    [Test]
    public void Dispose_StopsBackgroundSampling()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:9") };
        var reporter = new HostMetricsReporter(http);
        reporter.Start();
        reporter.Dispose();
    }
}
