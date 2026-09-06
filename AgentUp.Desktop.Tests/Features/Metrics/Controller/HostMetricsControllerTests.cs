using AgentUp.Desktop.Features.Metrics.Controllers;
using AgentUp.Desktop.Features.Metrics.Providers;
using AgentUp.Desktop.Features.Metrics.Services;

namespace AgentUp.Desktop.Tests.Features.Metrics.Controller;

[TestFixture]
public sealed class HostMetricsControllerTests
{
    [Test]
    public void StartAndStop_DoNotThrow()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:9") };
        using var controller = new HostMetricsController(new HostMetricsReporter(new HostMetricsApiClient(http)));

        controller.Start();
        controller.Stop();
    }
}
