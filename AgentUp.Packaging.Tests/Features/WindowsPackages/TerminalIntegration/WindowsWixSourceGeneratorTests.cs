using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.WindowsPackages;
using AgentUp.Packaging.Features.WindowsPackages.Models;

namespace AgentUp.Packaging.Tests.Features.WindowsPackages.TerminalIntegration;

[TestFixture]
public class WindowsWixSourceGeneratorTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Join(Path.GetTempPath(), "AgentUp-WixTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void ProductWxs_containsWindowsServicePathShortcutAndFiles()
    {
        var request = new PackageRequest(_root, "windows", "win-x64", "0.0.0-ci.149", "artifacts", "Release");
        var layout = WindowsPackageLayout.From(request);
        WritePublishedFile(layout.InstallerPublishDirectory, "AgentUp.InstallerApp.exe");
        WritePublishedFile(layout.DesktopPublishDirectory, "AgentUp.Desktop.exe");
        WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Server.exe");
        WritePublishedFile(layout.CliPublishDirectory, "AgentUp.CLI.exe");
        Directory.CreateDirectory(layout.InstallerSourceDirectory);
        File.WriteAllText(Path.Join(layout.InstallerSourceDirectory, "agent-up.cmd"), "");

        var xml = new WindowsWixSourceGenerator(WindowsPackageManifest.From(request)).ProductWxs(layout);

        Assert.That(xml, Does.Contain("Name=\"Agent-Up\""));
        Assert.That(xml, Does.Contain("Version=\"0.0.1\""));
        Assert.That(xml, Does.Contain("EmbedCab=\"yes\""));
        Assert.That(xml, Does.Contain("Id=\"ARPNOMODIFY\""));
        Assert.That(xml, Does.Contain("Id=\"ARPNOREPAIR\""));
        Assert.That(xml, Does.Contain("ServiceInstall"));
        Assert.That(xml, Does.Contain("Name=\"agent-up-server\""));
        Assert.That(xml, Does.Not.Contain("Start=\"install\""));
        Assert.That(xml, Does.Not.Contain("Stop=\"both\""));
        Assert.That(xml, Does.Contain("Stop=\"uninstall\""));
        Assert.That(xml, Does.Contain("Arguments=\"--urls http://127.0.0.1:5000\""));
        Assert.That(xml, Does.Contain("Name=\"PATH\""));
        Assert.That(xml, Does.Contain("Shortcut"));
        Assert.That(xml, Does.Contain("AgentUp.Desktop.exe"));
        Assert.That(xml, Does.Contain("AgentUp.Server.exe"));
        Assert.That(xml, Does.Contain("AgentUp.CLI.exe"));
    }

    [Test]
    public void BundleWxs_chainsGuidedInstallerWithBundledPayload()
    {
        var request = new PackageRequest(_root, "windows", "win-x64", "1.2.3", "artifacts", "Release");
        var layout = WindowsPackageLayout.From(request);
        WritePublishedFile(layout.InstallerPublishDirectory, "AgentUp.InstallerApp.exe");
        WritePublishedFile(layout.InstallerPublishDirectory, "support.dll");
        WritePublishedFile(layout.DesktopPublishDirectory, "AgentUp.Desktop.exe");
        WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Server.exe");
        WritePublishedFile(layout.CliPublishDirectory, "AgentUp.CLI.exe");

        var xml = new WindowsWixSourceGenerator(WindowsPackageManifest.From(request)).BundleWxs(layout);

        Assert.That(xml, Does.Contain("WixStandardBootstrapperApplication"));
        Assert.That(xml, Does.Contain("Theme=\"rtfLicense\""));
        Assert.That(xml, Does.Contain("ExePackage"));
        Assert.That(xml, Does.Contain("AgentUp.InstallerApp.exe"));
        Assert.That(xml, Does.Contain("DetectCondition=\"AgentUpInstalled\""));
        Assert.That(xml, Does.Contain("InstallArguments=\"--payload-root &quot;[WixBundleExecutePackageCacheFolder]payload&quot;\""));
        Assert.That(xml, Does.Contain("UninstallArguments=\"--uninstall\""));
        Assert.That(xml, Does.Contain("RegistrySearch"));
        Assert.That(xml, Does.Contain(@"CurrentVersion\Uninstall\Agent-Up"));
        Assert.That(xml, Does.Not.Contain("Permanent=\"yes\""));
        Assert.That(xml, Does.Not.Contain("MsiPackage"));
        Assert.That(xml, Does.Contain("Name=\"payload\\desktop\\AgentUp.Desktop.exe\""));
        Assert.That(xml, Does.Contain("Name=\"payload\\server\\AgentUp.Server.exe\""));
        Assert.That(xml, Does.Contain("Name=\"payload\\cli\\AgentUp.CLI.exe\""));
        Assert.That(xml, Does.Contain("Name=\"installer\\support.dll\""));
        Assert.That(xml, Does.Contain("http://wixtoolset.org/schemas/v4/wxs/bal"));
    }

    private static void WritePublishedFile(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, name), "test");
    }
}
