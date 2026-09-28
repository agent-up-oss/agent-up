using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Ports.Unit;

[TestFixture]
public sealed class PortSubTabViewModelTests
{
    [Test]
    public void SetLedState_Healthy_isGreenEvenWhenLoopbackIsClosed()
    {
        var tab = new PortSubTabViewModel("WEB_PORT", 3000, 9100);

        tab.SetLedState(PortLedState.Healthy);

        Assert.Multiple(() =>
        {
            Assert.That(tab.StatusColor, Is.EqualTo(AppHealthLedRules.StateColor("Healthy")));
            Assert.That(tab.IsOpen, Is.True);
        });
    }

    [Test]
    public void SetLedState_Checking_isNotAFailedProbe()
    {
        var tab = new PortSubTabViewModel("WEB_PORT", 3000, 9100);

        tab.SetLedState(PortLedState.Checking);

        Assert.That(tab.StatusColor, Is.EqualTo(AppHealthLedRules.StateColor("Checking")));
    }

    [Test]
    public void UnprobedPort_isMutedGreyNotFailedRed()
    {
        var tab = new PortSubTabViewModel("WEB_PORT", 3000, 9100);

        Assert.Multiple(() =>
        {
            Assert.That(tab.StatusColor, Is.EqualTo(AppHealthLedRules.StateColor("Stopped")));
            Assert.That(tab.StatusColor, Is.Not.EqualTo(AppHealthLedRules.StateColor("Failed")));
            Assert.That(tab.IsOpen, Is.False);
        });
    }

    [Test]
    public void UnhealthyPort_isRed()
    {
        var tab = new PortSubTabViewModel("WEB_PORT", 3000, 9100);

        tab.SetLedState(PortLedState.Unhealthy);

        Assert.That(tab.StatusColor, Is.EqualTo(AppHealthLedRules.StateColor("Unhealthy")));
    }
}
