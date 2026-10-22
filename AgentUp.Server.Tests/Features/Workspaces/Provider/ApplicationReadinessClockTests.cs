using AgentUp.Server.Features.Workspaces.Providers;

namespace AgentUp.Server.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class ApplicationReadinessClockTests
{
    [Test]
    public void UtcNow_tracksTheWallClock()
    {
        var before = DateTimeOffset.UtcNow;
        var now = new ApplicationReadinessClock().UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.That(now, Is.InRange(before, after));
    }

    [Test]
    public void Delay_throwsWhenTheCallerHasAlreadyCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.That(
            async () => await new ApplicationReadinessClock().Delay(TimeSpan.FromSeconds(5), cancelled.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
