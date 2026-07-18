using AgentUp.Installers.Features.MacOsInstallation.Services;

namespace AgentUp.Installers.Tests.Features.MacOsInstallation;

[TestFixture]
public class MacOsInstallerScriptsTests
{
    [Test]
    public void InstallerPreInstallScript_removesPreviousInstallerBundle()
    {
        var script = MacOsInstallerScripts.InstallerPreInstallScript();

        Assert.That(script, Does.Contain("rm -rf \"/Applications/Agent-Up Installer.app\""));
    }

    [Test]
    public void InstallerPostInstallScript_opensInstallerApp()
    {
        var script = MacOsInstallerScripts.InstallerPostInstallScript();

        Assert.That(script, Does.Contain("open -a \"/Applications/Agent-Up Installer.app\""));
    }
}
