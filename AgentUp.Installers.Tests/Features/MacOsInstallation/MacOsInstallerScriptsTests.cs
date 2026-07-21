using AgentUp.Installers.Features.MacOsInstallation.Models;

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
    public void InstallerPreInstallScript_cleansUpDotNetExtractionDirectory()
    {
        var script = MacOsInstallerScripts.InstallerPreInstallScript();

        Assert.That(script, Does.Contain(".net/AgentUp.InstallerApp"));
        Assert.That(script, Does.Contain("CONSOLE_USER"));
    }

    [Test]
    public void InstallerPostInstallScript_opensInstallerApp()
    {
        var script = MacOsInstallerScripts.InstallerPostInstallScript();

        Assert.That(script, Does.Contain("open -a \"/Applications/Agent-Up Installer.app\""));
    }
}
