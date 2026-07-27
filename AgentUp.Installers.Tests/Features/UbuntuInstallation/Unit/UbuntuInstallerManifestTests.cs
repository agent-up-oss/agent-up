using AgentUp.Installers.Features.UbuntuInstallation.Models;

namespace AgentUp.Installers.Tests.Features.UbuntuInstallation.Unit;

[TestFixture]
public class UbuntuInstallerManifestTests
{
    [Test]
    public void PostInstallScript_registersAndStartsService()
    {
        var script = UbuntuInstallerManifest.PostInstallScript();

        Assert.That(script, Does.Contain("systemctl enable --now agent-up-server.service"));
    }

    [Test]
    public void PostInstallScript_doesNotRunInstallCore()
    {
        var script = UbuntuInstallerManifest.PostInstallScript();

        Assert.That(script, Does.Not.Contain("--install-core"));
    }

    [Test]
    public void DesktopEntryText_declaresStartupWmClassForUbuntuTaskbarIcon()
    {
        var text = UbuntuInstallerManifest.AgentUp()
            .DesktopEntryText("/opt/agent-up/desktop/AgentUp.Desktop", "1.2.3");

        Assert.That(text, Does.Contain("Icon=agent-up"));
        Assert.That(text, Does.Contain("StartupWMClass=AgentUp.Desktop"));
    }
}
