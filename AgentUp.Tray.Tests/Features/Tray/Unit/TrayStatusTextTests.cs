using AgentUp.Tray.Features.Tray;

namespace AgentUp.Tray.Tests.Features.Tray.Unit;

[TestFixture]
public sealed class TrayStatusTextTests
{
    [TestCase(ServiceState.Connecting, "Connecting...")]
    [TestCase(ServiceState.Connected, "Running")]
    [TestCase(ServiceState.Restarting, "Restarting...")]
    [TestCase(ServiceState.Disconnected, "Disconnected")]
    public void Word_readsAsAUserFacingStatus(ServiceState state, string expected)
    {
        Assert.That(TrayStatusText.Word(state), Is.EqualTo(expected));
    }

    [Test]
    public void MenuLine_prefixesTheProductName()
    {
        Assert.That(TrayStatusText.MenuLine(ServiceState.Connected), Is.EqualTo("Agent-Up  Running"));
    }

    [Test]
    public void ToolTip_separatesTheProductNameWithAMiddot()
    {
        Assert.That(TrayStatusText.ToolTip(ServiceState.Disconnected), Is.EqualTo("Agent-Up · Disconnected"));
    }

    [TestCase(ServiceState.Connected, true)]
    [TestCase(ServiceState.Connecting, false)]
    [TestCase(ServiceState.Restarting, false)]
    [TestCase(ServiceState.Disconnected, false)]
    public void CanRestart_isOfferedOnlyWhenTheServiceIsReachable(ServiceState state, bool expected)
    {
        Assert.That(TrayStatusText.CanRestart(state), Is.EqualTo(expected));
    }

    [Test]
    public void EveryStateHasDistinctWording()
    {
        var words = Enum.GetValues<ServiceState>().Select(TrayStatusText.Word).ToArray();

        Assert.That(words, Is.Unique);
    }

    [Test]
    public void Word_fallsBackForAStateTheWordingDoesNotKnow()
    {
        // Reached only by a value outside the enum, which is what the defensive arm is for:
        // a state added without wording must not surface as an empty menu line.
        Assert.That(TrayStatusText.Word((ServiceState)99), Is.EqualTo("Unknown"));
    }
}
