using AgentUp.Server.Features.TraySession.Services;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Tests.Features.TraySession.Unit;

[TestFixture]
public class TrayHeartbeatMonitorTests
{
    [Test]
    public async Task StartAsync_doesNotStop_whenNoHeartbeatReceived()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var monitor = Monitor(lifetime);

        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await monitor.StopAsync(CancellationToken.None);

        Assert.That(lifetime.StopRequested, Is.False);
    }

    [Test]
    public async Task RegisterHeartbeat_thenTimeout_stopsApplication()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var monitor = Monitor(lifetime);

        await monitor.StartAsync(CancellationToken.None);
        monitor.RegisterHeartbeat();

        await Task.Delay(500);

        Assert.That(lifetime.StopRequested, Is.True);
    }

    [Test]
    public async Task RegisterHeartbeat_renewedBeforeTimeout_doesNotStop()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var monitor = Monitor(lifetime, timeout: TimeSpan.FromMilliseconds(100), check: TimeSpan.FromMilliseconds(20));

        await monitor.StartAsync(CancellationToken.None);

        for (var i = 0; i < 8; i++)
        {
            monitor.RegisterHeartbeat();
            await Task.Delay(30); // 30ms apart, well within 100ms timeout
        }

        Assert.That(lifetime.StopRequested, Is.False);
        await monitor.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_cancelsPendingMonitor()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var monitor = Monitor(lifetime);

        await monitor.StartAsync(CancellationToken.None);
        monitor.RegisterHeartbeat();
        await monitor.StopAsync(CancellationToken.None); // cancel before timeout fires

        Assert.That(lifetime.StopRequested, Is.False);
    }

    private static TrayHeartbeatMonitor Monitor(
        IHostApplicationLifetime lifetime,
        TimeSpan? timeout = null,
        TimeSpan? check = null)
        => new(lifetime,
            timeout ?? TimeSpan.FromMilliseconds(50),
            check ?? TimeSpan.FromMilliseconds(10));

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopRequested = true;
    }
}
