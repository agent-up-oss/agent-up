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
}
