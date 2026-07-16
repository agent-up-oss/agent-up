using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.Windows;

namespace AgentUp.Packaging.Tests.Features.Windows;

[TestFixture]
public class WindowsWixSourceGeneratorTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "AgentUp-WixTests", Guid.NewGuid().ToString());
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
        var request = new PackageRequest(_root, "windows", "win-x64", "1.2.3", "artifacts", "Release");
        var layout = WindowsPackageLayout.From(request);
        WritePublishedFile(layout.InstallerPublishDirectory, "AgentUp.InstallerApp.exe");
        WritePublishedFile(layout.DesktopPublishDirectory, "AgentUp.Desktop.exe");
        WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Server.exe");
        WritePublishedFile(layout.CliPublishDirectory, "AgentUp.CLI.exe");
        Directory.CreateDirectory(layout.InstallerSourceDirectory);
        File.WriteAllText(Path.Combine(layout.InstallerSourceDirectory, "agent-up.cmd"), "");

        var xml = new WindowsWixSourceGenerator(WindowsPackageManifest.From(request)).ProductWxs(layout);

        Assert.That(xml, Does.Contain("Name=\"Agent-Up\""));
        Assert.That(xml, Does.Contain("ServiceInstall"));
        Assert.That(xml, Does.Contain("Name=\"agent-up-server\""));
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
        File.WriteAllText(Path.Combine(directory, name), "test");
    }
}
