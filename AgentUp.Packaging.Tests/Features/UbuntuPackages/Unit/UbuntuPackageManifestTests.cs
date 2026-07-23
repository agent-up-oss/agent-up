using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages.Models;

namespace AgentUp.Packaging.Tests.Features.UbuntuPackages.Unit;

[TestFixture]
public class UbuntuPackageManifestTests
{
    [Test]
    public void From_normalizesVersionAndDefinesNativeTargets()
    {
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "v1.2.3", "artifacts", "Release");

        var manifest = UbuntuPackageManifest.From(request);

        Assert.That(manifest.Version, Is.EqualTo("1.2.3"));
        Assert.That(manifest.PackageName, Is.EqualTo("agent-up"));
        Assert.That(manifest.ApplicationName, Is.EqualTo("Agent-Up"));
        Assert.That(manifest.ServiceName, Is.EqualTo("agent-up-server.service"));
    }

    [Test]
    public void MetainfoText_containsAppStreamComponentWithVersionAndPackageName()
    {
        var manifest = UbuntuPackageManifest.From(new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release"));

        var text = manifest.MetainfoText();

        Assert.That(text, Does.Contain("<pkgname>agent-up</pkgname>"));
        Assert.That(text, Does.Contain("<release version=\"1.2.3\""));
        Assert.That(text, Does.Contain("agent-up-installer.desktop"));
        Assert.That(text, Does.EndWith(Environment.NewLine));
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
}
