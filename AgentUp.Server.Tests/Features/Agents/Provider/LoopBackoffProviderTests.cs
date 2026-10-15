using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class LoopBackoffProviderTests
{
    [Test]
    public async Task DelayAsync_waitsAtLeastTheConfiguredInitialInterval()
    {
        var backoff = new LoopBackoffProvider(initialMilliseconds: 20, capMilliseconds: 40);
        var started = DateTime.UtcNow;

        await backoff.DelayAsync(1, CancellationToken.None);

        Assert.That(DateTime.UtcNow - started, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(15)));
    }

    [Test]
    public async Task DelayAsync_isCancelledWhenTheCallerCancels()
    {
        var backoff = new LoopBackoffProvider(initialMilliseconds: 10_000);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(() => backoff.DelayAsync(1, cancelled.Token));
    }
}
