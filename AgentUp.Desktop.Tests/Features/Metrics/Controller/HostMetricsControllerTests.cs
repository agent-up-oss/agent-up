using AgentUp.Desktop.Features.Metrics.Controllers;

namespace AgentUp.Desktop.Tests.Features.Metrics.Controller;

[TestFixture]
public sealed class HostMetricsControllerTests
{
    [Test]
    public void StartAndStop_DoNotThrow()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:9") };
        using var controller = new HostMetricsController(http);

        controller.Start();
        controller.Stop();
    }
}
