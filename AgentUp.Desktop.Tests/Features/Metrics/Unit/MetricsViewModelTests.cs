using System.Net;
using System.Net.Http.Json;
using AgentUp.Desktop.Features.Metrics.Controllers;
using AgentUp.Desktop.Features.Metrics.DTOs;
using AgentUp.Desktop.Features.Metrics.Providers;
using AgentUp.Desktop.Features.Metrics.Services;
using AgentUp.Desktop.Features.Metrics.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Metrics.Unit;

[TestFixture]
public sealed class MetricsViewModelTests
{
    [Test]
    public async Task LoadAsync_PopulatesSummaryAndCharts()
    {
        var timeline = new ApplicationMetricsTimelineDto(
            [
                new MetricsSummaryCardDto("Latency", "142ms", "ms"),
                new MetricsSummaryCardDto("Req/min", "1.2k", null)
            ],
            [
                new MetricsSeriesDto(
                    "latency_ms",
                    "latency ms",
                    "ms",
                    [
                        new MetricsPointDto(DateTimeOffset.UtcNow.AddMinutes(-1), 120),
                        new MetricsPointDto(DateTimeOffset.UtcNow, 142)
                    ]),
                new MetricsSeriesDto(
                    "requests_per_minute",
                    "requests per minute",
                    "/min",
                    [
                        new MetricsPointDto(DateTimeOffset.UtcNow.AddMinutes(-1), 900),
                        new MetricsPointDto(DateTimeOffset.UtcNow, 1200)
                    ])
            ]);

        var http = new HttpClient(new MetricsHttpHandler(timeline))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var vm = new MetricsViewModel(new MetricsController(new MetricsTimelineService(new MetricsApiClient(http))));

        await vm.LoadAsync("ws-1", "Web");

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasSummary, Is.True);
            Assert.That(vm.HasCharts, Is.True);
            Assert.That(vm.SummaryCards, Has.Count.EqualTo(2));
            Assert.That(vm.Charts, Has.Count.EqualTo(2));
            Assert.That(vm.Charts[0].Points, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task LoadAsync_SkipsCharts_WhenAllValuesAreZero()
    {
        var timeline = new ApplicationMetricsTimelineDto(
            [
                new MetricsSummaryCardDto("Latency", "—", "ms"),
                new MetricsSummaryCardDto("Req/min", "0", null)
            ],
            [
                new MetricsSeriesDto(
                    "latency_ms",
                    "latency ms",
                    "ms",
                    [
                        new MetricsPointDto(DateTimeOffset.UtcNow.AddMinutes(-1), 0),
                        new MetricsPointDto(DateTimeOffset.UtcNow, 0)
                    ]),
                new MetricsSeriesDto(
                    "requests_per_minute",
                    "requests per minute",
                    "/min",
                    [
                        new MetricsPointDto(DateTimeOffset.UtcNow, 0)
                    ])
            ]);

        var http = new HttpClient(new MetricsHttpHandler(timeline))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var vm = new MetricsViewModel(new MetricsController(new MetricsTimelineService(new MetricsApiClient(http))));

        await vm.LoadAsync("ws-1", "Web");

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasSummary, Is.True);
            Assert.That(vm.HasCharts, Is.False);
            Assert.That(vm.Charts, Is.Empty);
        });
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
