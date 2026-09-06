using System.Net;
using System.Net.Http.Json;
using AgentUp.Desktop.Features.Metrics.Controllers;
using AgentUp.Desktop.Features.Metrics.DTOs;
using AgentUp.Desktop.Features.Metrics.Providers;
using AgentUp.Desktop.Features.Metrics.Services;

namespace AgentUp.Desktop.Tests.Features.Metrics.Controller;

[TestFixture]
public sealed class MetricsControllerTests
{
    [Test]
    public async Task GetTimelineAsync_DelegatesToService()
    {
        var timeline = new ApplicationMetricsTimelineDto(
            [new MetricsSummaryCardDto("Latency", "120ms", "ms")],
            []);
        var http = new HttpClient(new MetricsHttpHandler(timeline))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var controller = new MetricsController(new MetricsTimelineService(new MetricsApiClient(http)));

        var result = await controller.GetTimelineAsync("ws-1", "Web");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Summary, Has.Count.EqualTo(1));
    }

    private sealed class MetricsHttpHandler(ApplicationMetricsTimelineDto timeline) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(timeline)
            });
    }
}
