using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Metrics.ViewModels;
using AgentUp.Desktop.Features.Metrics.Views;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Metrics.Headless;

[TestFixture]
public sealed class MetricsTimeSeriesChartTests
{
    [AvaloniaTest]
    public async Task Render_skipsPlotWhenThereAreNoPoints()
    {
        var app = await AppDriver.LaunchEmptyAsync();
        var chart = new MetricsTimeSeriesChart
        {
            Width = 240,
            Height = 190,
            Points = []
        };
        app.Window.Content = chart;
        await HeadlessExtensions.FlushAsync();

        Assert.That(chart.Bounds.Width, Is.GreaterThan(1));
        Assert.That(chart.Bounds.Height, Is.GreaterThan(1));
    }

    [AvaloniaTest]
    public async Task Render_drawsAxisAndBarsWhenPointsArePresent()
    {
        var app = await AppDriver.LaunchEmptyAsync();
        var chart = new MetricsTimeSeriesChart
        {
            Width = 240,
            Height = 190,
            Unit = "ms",
            Points =
            [
                new MetricsPointViewModel(DateTimeOffset.UtcNow, 10),
                new MetricsPointViewModel(DateTimeOffset.UtcNow, 0),
                new MetricsPointViewModel(DateTimeOffset.UtcNow, 40)
            ]
        };
        app.Window.Content = chart;
        await HeadlessExtensions.FlushAsync();

        Assert.That(chart.Bounds.Width, Is.GreaterThan(1));
        Assert.That(chart.Points, Has.Count.EqualTo(3));
        Assert.That(chart.Unit, Is.EqualTo("ms"));
    }
}
