using System.Xml.Linq;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.UbuntuPackages.Models;
using LocalInstaller.Packaging.Tests.Support;

namespace LocalInstaller.Packaging.Tests.Features.UbuntuPackages.Unit;

[TestFixture]
public class UbuntuPackageManifestTests
{
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\pkg" : "/pkg";
    [Test]
    public void From_normalizesVersionAndDefinesNativeTargets()
    {
        var request = new PackageRequest(Root, "ubuntu", "linux-x64", "v1.2.3", "artifacts", "Release", AgentUpPackageTestManifests.Product());

        var manifest = UbuntuPackageManifest.From(request);

        Assert.That(manifest.Version, Is.EqualTo("1.2.3"));
        Assert.That(manifest.PackageName, Is.EqualTo("agent-up"));
        Assert.That(manifest.ApplicationName, Is.EqualTo("Agent-Up"));
        Assert.That(manifest.ServiceName, Is.EqualTo("agent-up-server.service"));
        Assert.That(manifest.InstallerExecutableName, Is.EqualTo("AgentUp.InstallerApp"));
    }

    [Test]
    public void MetainfoText_containsAppStreamComponentWithVersionAndPackageName()
    {
        var manifest = UbuntuPackageManifest.From(new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release", AgentUpPackageTestManifests.Product()));

        var text = manifest.MetainfoText();

        Assert.That(text, Does.Contain("<pkgname>agent-up</pkgname>"));
        Assert.That(text, Does.Contain("<release version=\"1.2.3\""));
        Assert.That(text, Does.Contain("agent-up-installer.desktop"));
        Assert.That(text, Does.EndWith(Environment.NewLine));
        Assert.DoesNotThrow(() => XDocument.Parse(text.Trim()), "MetainfoText must be valid XML");
    }

    [Test]
    public void ControlFileText_containsDebianPackageMetadata()
    {
        var manifest = UbuntuPackageManifest.From(new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release", AgentUpPackageTestManifests.Product()));

        var text = manifest.ControlFileText();

        Assert.That(text, Does.Contain("Package: agent-up"));
        Assert.That(text, Does.Contain("Version: 1.2.3"));
        Assert.That(text, Does.Contain("Architecture: amd64"));
        Assert.That(text, Does.EndWith(Environment.NewLine));
    }

    [Test]
    public void PostInstallScript_updatesLauncherMetadataWithoutLaunchingInstallerDashboard()
    {
        var manifest = UbuntuPackageManifest.From(new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release", AgentUpPackageTestManifests.Product()));

        var text = manifest.PostInstallScript();

        Assert.That(text, Does.Contain("update-desktop-database"));
        Assert.That(text, Does.Not.Contain("su \"$SUDO_USER\""));
        Assert.That(text, Does.Not.Contain("AgentUp.InstallerApp &"));
    }

    [Test]
    public void InstallerDesktopEntryText_declaresStartupWmClassForUbuntuTaskbarIcon()
    {
        var manifest = UbuntuPackageManifest.From(new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release", AgentUpPackageTestManifests.Product()));

        var text = manifest.InstallerDesktopEntryText();

        Assert.That(text, Does.Contain("Icon=agent-up"));
        Assert.That(text, Does.Contain("StartupWMClass=AgentUp.InstallerApp"));
    }

    [Test]
    public void From_usesRegisteredInstallerApplicationExecutable()
    {
        var product = new PackageProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK")
        {
            InstallerApplication = new PackageProductArtifact(
                "orbit-installer",
                "Installer",
                "",
                "Orbit.Installer",
                "Orbit.Installer/Orbit.Installer.csproj",
                "installer",
                LocalInstaller.Core.Shared.Models.LocalInstallerArtifactTarget.InstallerApp)
        };
        var manifest = UbuntuPackageManifest.From(new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release", product));

        Assert.That(manifest.InstallerExecutableName, Is.EqualTo("Orbit.Installer"));
        Assert.That(manifest.PostInstallScript(), Does.Contain("chmod +x /opt/orbit-desk/installer/Orbit.Installer"));
        Assert.That(manifest.InstallerDesktopEntryText(), Does.Contain("Exec=/opt/orbit-desk/installer/Orbit.Installer"));
        Assert.That(manifest.InstallerDesktopEntryText(), Does.Contain("StartupWMClass=Orbit.Installer"));
    }
}
