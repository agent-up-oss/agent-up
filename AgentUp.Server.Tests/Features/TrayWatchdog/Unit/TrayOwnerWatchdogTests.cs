using AgentUp.Server.Features.TrayWatchdog.Services;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Tests.Features.TrayWatchdog.Unit;

[TestFixture]
public class TrayOwnerWatchdogTests
{
    [Test]
    public async Task StartAsync_doesNothing_whenTrayPidEnvVarIsNotSet()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var watchdog = new TrayOwnerWatchdog(lifetime, _ => null);

        await watchdog.StartAsync(CancellationToken.None);
        await watchdog.StopAsync(CancellationToken.None);

        Assert.That(lifetime.StopRequested, Is.False);
    }

    [Test]
    public async Task StartAsync_doesNothing_whenTrayPidEnvVarIsNotAnInteger()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var watchdog = new TrayOwnerWatchdog(lifetime, _ => "not-a-pid");

        await watchdog.StartAsync(CancellationToken.None);
        await watchdog.StopAsync(CancellationToken.None);

        Assert.That(lifetime.StopRequested, Is.False);
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => StopRequested = true;
    }
}
