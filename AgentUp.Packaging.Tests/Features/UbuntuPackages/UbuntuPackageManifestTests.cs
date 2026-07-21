using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.UbuntuPackages;
using AgentUp.Installers.Features.UbuntuInstallation;
using AgentUp.Installers.Features.UbuntuInstallation.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages.Models;

namespace AgentUp.Packaging.Tests.Features.UbuntuPackages;

[TestFixture]
public class UbuntuPackageManifestTests
{
    [Test]
    public void From_normalizesVersionAndDefinesNativeTargets()
    {
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "v1.2.3", "artifacts", "Release");

        var manifest = UbuntuPackageManifest.From(request);

        var installerManifest = UbuntuInstallerManifest.AgentUp();
        var installerPaths = UbuntuInstallerPaths.ForProduct(installerManifest);
        Assert.That(manifest.Version, Is.EqualTo("1.2.3"));
        Assert.That(manifest.PackageName, Is.EqualTo(installerManifest.PackageName));
        Assert.That(manifest.ServiceName, Is.EqualTo(installerManifest.ServiceUnitName));
        Assert.That(manifest.CliSymlinkTarget, Is.EqualTo(installerPaths.CliExecutable));
        Assert.That(manifest.DesktopEntryPath, Is.EqualTo(installerPaths.DesktopEntryPath));
        Assert.That(manifest.IconPath, Is.EqualTo(installerPaths.IconPath));
    }

    [Test]
    public void ControlFileText_containsDebianPackageMetadata()
    {
        var manifest = UbuntuPackageManifest.From(new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release"));

        var text = manifest.ControlFileText();

        Assert.That(text, Does.Contain("Package: agent-up"));
        Assert.That(text, Does.Contain("Version: 1.2.3"));
        Assert.That(text, Does.Contain("Architecture: amd64"));
        Assert.That(text, Does.EndWith(Environment.NewLine));
    }

    [Test]
    public void DesktopEntryText_registersDesktopApplication()
    {
        var manifest = UbuntuPackageManifest.From(new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release"));

        var text = manifest.DesktopEntryText();

        Assert.That(text, Does.Contain("Name=Agent-Up"));
        Assert.That(text, Does.Contain("Exec=/opt/agent-up/desktop/AgentUp.Desktop"));
        Assert.That(text, Does.Contain("Icon=agent-up"));
        Assert.That(text, Does.Contain("X-AgentUp-Version=1.2.3"));
    }
}
