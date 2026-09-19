using AgentUp.Desktop.Features.Metrics.Providers;

namespace AgentUp.Desktop.Tests.Features.Metrics.Provider;

[TestFixture]
public sealed class ProcessMetricsSamplerTests
{
    [Test]
    public async Task SampleAsync_ReturnsProcessMetrics()
    {
        var metrics = await ProcessMetricsSampler.SampleAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(metrics.ContainsKey("process.cpuPercent"), Is.True);
            Assert.That(metrics.ContainsKey("process.workingSetBytes"), Is.True);
            Assert.That(metrics.ContainsKey("process.gcHeapBytes"), Is.True);
        });
    }

    [Test]
    public async Task SampleAsync_returns_non_negative_process_measurements()
    {
        var metrics = await ProcessMetricsSampler.SampleAsync(TimeSpan.Zero, CancellationToken.None);

        Assert.That(metrics.Values.Select(value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)),
            Has.All.GreaterThanOrEqualTo(0));
    }
}
