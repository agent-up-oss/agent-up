using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class LoopOutcomeBaselineStoreTests
{
    [Test]
    public void Read_loadsTheVersionedPooledRate()
    {
        var path = Path.Join(Directory.CreateTempSubdirectory("agent-up-loop-baseline").FullName, "baseline.json");
        File.WriteAllText(path,
            """{"model":"qwen","pooledSuccessRate":0.5,"tolerance":0.25,"samples":80,"recordedAt":"2026-10-12"}""");

        var baseline = new LoopOutcomeBaselineStore().Read(path);

        Assert.Multiple(() =>
        {
            Assert.That(baseline.PooledSuccessRate, Is.EqualTo(0.5));
            Assert.That(baseline.Tolerance, Is.EqualTo(0.25));
        });
    }

    [Test]
    public void WithinTolerance_failsOnlyOnASignificantDrop()
    {
        var baseline = new LoopOutcomeBaseline( "qwen", 0.50, 0.20, 80, "2026-10-12");
        var store = new LoopOutcomeBaselineStore();

        Assert.Multiple(() =>
        {
            Assert.That(store.WithinTolerance(baseline, 0.40), Is.True);
            Assert.That(store.WithinTolerance(baseline, 0.39), Is.False);
        });
    }
}
