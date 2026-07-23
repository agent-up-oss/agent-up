using AgentUp.Packaging.Features.MacOsPackages.Models;

namespace AgentUp.Packaging.Tests.Features.MacOsPackages.Unit;

[TestFixture]
public class MacOsScriptGeneratorTests
{
    [Test]
    public void InstallerPreInstallScript_removesPreviousInstallerBundle()
    {
        var script = MacOsScriptGenerator.InstallerPreInstallScript();

        Assert.That(script, Does.Contain("rm -rf \"/Applications/Agent-Up Installer.app\""));
    }

    [Test]
    public void InstallerPreInstallScript_cleansUpDotNetExtractionDirectory()
    {
        var script = MacOsScriptGenerator.InstallerPreInstallScript();

        Assert.That(script, Does.Contain(".net/AgentUp.InstallerApp"));
        Assert.That(script, Does.Contain("CONSOLE_USER"));
    }

    [Test]
    public void InstallerPreInstallScript_validatesConsoleUserBeforeDeletion()
    {
        var script = MacOsScriptGenerator.InstallerPreInstallScript();

        Assert.That(script, Does.Contain("[[ \"$CONSOLE_USER\" =~ ^[a-zA-Z0-9._-]+$ ]]"));
    }

    [Test]
    public void InstallerPostInstallScript_onlyOpensGui()
    {
        var script = MacOsScriptGenerator.InstallerPostInstallScript();

        Assert.That(script, Does.Contain("open -a \"/Applications/Agent-Up Installer.app\""));
        Assert.That(script, Does.Not.Contain("--install-core"));
    }
}
