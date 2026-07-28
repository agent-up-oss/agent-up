using AgentUp.Packaging.Features.MacOsPackages.Models;

namespace AgentUp.Packaging.Tests.Features.MacOsPackages.Unit;

[TestFixture]
public class MacOsScriptGeneratorTests
{
    [Test]
    public void InstallerPreInstallScript_removesPreviousInstallerBundle()
    {
        var script = MacOsScriptGenerator.InstallerPreInstallScript("Agent-Up Installer");

        Assert.That(script, Does.Contain("rm -rf \"/Applications/Agent-Up Installer.app\""));
    }

    [Test]
    public void InstallerPreInstallScript_cleansUpDotNetExtractionDirectory()
    {
        var script = MacOsScriptGenerator.InstallerPreInstallScript("Agent-Up Installer");

        Assert.That(script, Does.Contain(".net/AgentUp.InstallerApp"));
        Assert.That(script, Does.Contain("CONSOLE_USER"));
    }

    [Test]
    public void InstallerPreInstallScript_validatesConsoleUserBeforeDeletion()
    {
        var script = MacOsScriptGenerator.InstallerPreInstallScript("Agent-Up Installer");

        Assert.That(script, Does.Contain("[[ \"$CONSOLE_USER\" =~ ^[a-zA-Z0-9._-]+$ ]]"));
    }

    [Test]
    public void InstallerPostInstallScript_onlyOpensGui()
    {
        var script = MacOsScriptGenerator.InstallerPostInstallScript("Agent-Up Installer", "/Library/Logs/Agent-Up");

        Assert.That(script, Does.Contain("open -a \"/Applications/Agent-Up Installer.app\""));
        Assert.That(script, Does.Contain("installer-startup.err"));
        Assert.That(script, Does.Not.Contain("--install-core"));
    }

    [Test]
    public void InstallerPreInstallScript_withNonDefaultBranding_usesProvidedAppName()
    {
        var script = MacOsScriptGenerator.InstallerPreInstallScript("Acme Studio Installer");

        Assert.That(script, Does.Contain("rm -rf \"/Applications/Acme Studio Installer.app\""));
        Assert.That(script, Does.Not.Contain("Agent-Up"));
    }

    [Test]
    public void InstallerPostInstallScript_withNonDefaultBranding_opensProvidedAppAndWritesToProductLogDir()
    {
        var script = MacOsScriptGenerator.InstallerPostInstallScript("Acme Studio Installer", "/Library/Logs/Acme Studio");

        Assert.That(script, Does.Contain("open -a \"/Applications/Acme Studio Installer.app\""));
        Assert.That(script, Does.Contain("/Library/Logs/Acme Studio/installer-startup.err"));
        Assert.That(script, Does.Not.Contain("Agent-Up"));
    }
}
