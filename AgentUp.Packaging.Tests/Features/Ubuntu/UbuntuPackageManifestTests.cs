using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.Ubuntu;
using AgentUp.Installers.Features.Ubuntu;

namespace AgentUp.Packaging.Tests.Features.Ubuntu;

[TestFixture]
public class UbuntuPackageManifestTests
{
    [Test]
    public void From_normalizesVersionAndDefinesNativeTargets()
    {
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "v1.2.3", "artifacts", "Release");

        var manifest = UbuntuPackageManifest.From(request);

        Assert.That(manifest.Version, Is.EqualTo("1.2.3"));
        Assert.That(manifest.PackageName, Is.EqualTo(UbuntuInstallerManifest.PackageName));
        Assert.That(manifest.ServiceName, Is.EqualTo(UbuntuInstallerManifest.ServiceName));
        Assert.That(manifest.CliSymlinkTarget, Is.EqualTo(UbuntuInstallerPaths.SystemDefault().CliExecutable));
        Assert.That(manifest.DesktopEntryPath, Is.EqualTo(UbuntuInstallerPaths.SystemDefault().DesktopEntryPath));
        Assert.That(manifest.IconPath, Is.EqualTo(UbuntuInstallerPaths.SystemDefault().IconPath));
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
