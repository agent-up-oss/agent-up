using AgentUp.Installers.Features.UbuntuInstallation.Models;

namespace AgentUp.Installers.Tests.Features.UbuntuInstallation;

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
}
