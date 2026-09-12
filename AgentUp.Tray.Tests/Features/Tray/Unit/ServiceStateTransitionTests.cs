using AgentUp.Tray.Features.Tray;

namespace AgentUp.Tray.Tests.Features.Tray.Unit;

[TestFixture]
public sealed class ServiceStateTransitionTests
{
    [TestCase(ServiceState.Connecting)]
    [TestCase(ServiceState.Connected)]
    [TestCase(ServiceState.Disconnected)]
    public void Next_reportsConnectedWhenAPollSucceeds(ServiceState current)
    {
        Assert.That(ServiceStateTransition.Next(current, pollSucceeded: true),
            Is.EqualTo(ServiceState.Connected));
    }

    [Test]
    public void Next_ignoresASuccessfulPollDuringARestart()
    {
        // The restart is not finished until a later poll; reporting Connected mid-restart
        // would flicker the menu back and forth.
        Assert.That(ServiceStateTransition.Next(ServiceState.Restarting, pollSucceeded: true), Is.Null);
    }

    [TestCase(ServiceState.Connected)]
    [TestCase(ServiceState.Restarting)]
    public void Next_reportsDisconnectedWhenAPollFailsFromAReachableState(ServiceState current)
    {
        Assert.That(ServiceStateTransition.Next(current, pollSucceeded: false),
            Is.EqualTo(ServiceState.Disconnected));
    }

    [TestCase(ServiceState.Connecting)]
    [TestCase(ServiceState.Disconnected)]
    public void Next_changesNothingWhenAPollFailsAndTheServerWasNeverReachable(ServiceState current)
    {
        // Staying in Connecting matters on startup: the tray should not claim the server
        // disconnected before it was ever seen.
        Assert.That(ServiceStateTransition.Next(current, pollSucceeded: false), Is.Null);
    }

    [TestCase(ServiceState.Connected, true)]
    [TestCase(ServiceState.Connecting, false)]
    [TestCase(ServiceState.Restarting, false)]
    [TestCase(ServiceState.Disconnected, false)]
    public void ShouldHeartbeat_onlyWhileConnected(ServiceState current, bool expected)
    {
        Assert.That(ServiceStateTransition.ShouldHeartbeat(current), Is.EqualTo(expected));
    }
}
