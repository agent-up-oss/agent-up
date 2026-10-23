using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Authentication.Unit;

public sealed class SavedServerDtoTests
{
    [Test]
    public void SavedServer_exposesStableSelectAndRemoveAutomationIds()
    {
        var server = new SavedServerDto("srv-1", "http://127.0.0.1:5000", true, false, "Local", true, false, false);

        Assert.Multiple(() =>
        {
            Assert.That(server.SelectAutomationId, Is.EqualTo(DesktopAutomationIds.LoginSaved("srv-1")));
            Assert.That(server.RemoveAutomationId, Is.EqualTo(DesktopAutomationIds.LoginRemoveSaved("srv-1")));
        });
    }
}
