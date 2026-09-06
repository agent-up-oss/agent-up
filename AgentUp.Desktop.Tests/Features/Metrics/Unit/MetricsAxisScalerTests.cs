using AgentUp.Desktop.Features.Metrics.Views;

namespace AgentUp.Desktop.Tests.Features.Metrics.Unit;

[TestFixture]
public sealed class MetricsAxisScalerTests
{
    [Test]
    public void Compute_RoundsUpToReadableSteps_FromZero()
    {
        var (max, step) = MetricsAxisScaler.Compute(1200);

        Assert.Multiple(() =>
        {
            Assert.That(max, Is.GreaterThanOrEqualTo(1200));
            Assert.That(step, Is.GreaterThan(0));
            Assert.That(max % step, Is.EqualTo(0));
        });
    }

    [Test]
    public void FormatTick_UsesCompactThousands_ForLargeCounts()
    {
        Assert.That(MetricsAxisScaler.FormatTick(1500, string.Empty), Is.EqualTo("1.5k"));
    }

    [Test]
    public void FormatTick_AppendsPercent_ForPercentUnit()
    {
        Assert.That(MetricsAxisScaler.FormatTick(99.5, "%"), Is.EqualTo("100%"));
    }
}
