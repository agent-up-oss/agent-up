using AgentUp.Tray.Features.Tray;

namespace AgentUp.Tray.Tests.Features.Tray.Unit;

[TestFixture]
public sealed class ServiceStateTransitionTests
{
    [TestCase(ServiceState.Connecting)]
    [TestCase(ServiceState.Connected)]
    [TestCase(ServiceState.Disconnected)]
    [TestCase(ServiceState.Restarting)]
    public void Next_reportsConnectedWhenAPollSucceeds(ServiceState current)
    {
        Assert.That(ServiceStateTransition.Next(current, pollSucceeded: true),
            Is.EqualTo(ServiceState.Connected));
    }

    [Test]
    public void Next_leavesRestartingAsSoonAsTheServerAnswers()
    {
        // A restart quick enough that no poll ever fails must not strand the tray. Holding
        // Restarting until a failure did: the label stayed "Restarting..." and the restart
        // item, offered only while Connected, stayed disabled for the life of the process.
        var afterRestart = ServiceStateTransition.Next(ServiceState.Restarting, pollSucceeded: true);

        Assert.Multiple(() =>
        {
            Assert.That(afterRestart, Is.EqualTo(ServiceState.Connected));
            Assert.That(ServiceStateTransition.ShouldHeartbeat(afterRestart!.Value), Is.True,
                "Heartbeats have to resume, or the server stops seeing the tray.");
        });
    }

    [Test]
    public void Next_recoversFromARestartThatTookTheServerDown()
    {
        // The other restart shape: the server stops answering, then comes back.
        var down = ServiceStateTransition.Next(ServiceState.Restarting, pollSucceeded: false);
        var up = ServiceStateTransition.Next(down!.Value, pollSucceeded: true);

        Assert.Multiple(() =>
        {
            Assert.That(down, Is.EqualTo(ServiceState.Disconnected));
            Assert.That(up, Is.EqualTo(ServiceState.Connected));
        });
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
