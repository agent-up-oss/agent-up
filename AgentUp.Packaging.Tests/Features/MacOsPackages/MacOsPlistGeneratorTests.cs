using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Tests.Features.MacOsPackages;

[TestFixture]
public class MacOsPlistGeneratorTests
{
    [Test]
    public void DesktopInfoPlist_containsBundleMetadataAndVersion()
    {
        var manifest = MacOsPackageManifest.From(new PackageRequest("/repo", "macos", "osx-arm64", "v1.2.3", "artifacts", "Release"));

        var plist = new MacOsPlistGenerator(manifest).DesktopInfoPlist();

        Assert.That(plist, Does.Contain("CFBundleIdentifier"));
        Assert.That(plist, Does.Contain("dev.agent-up.desktop"));
        Assert.That(plist, Does.Contain("CFBundleExecutable"));
        Assert.That(plist, Does.Contain("AgentUp.Desktop"));
        Assert.That(plist, Does.Contain("CFBundleIconFile"));
        Assert.That(plist, Does.Contain("Agent-Up.png"));
        Assert.That(plist, Does.Contain("CFBundleShortVersionString"));
        Assert.That(plist, Does.Contain("1.2.3"));
    }

    [Test]
    public void InstallerInfoPlist_containsInstallerExecutableAndVersion()
    {
        var manifest = MacOsPackageManifest.From(new PackageRequest("/repo", "macos", "osx-arm64", "v1.2.3", "artifacts", "Release"));

        var plist = new MacOsPlistGenerator(manifest).InstallerInfoPlist();

        Assert.That(plist, Does.Contain("dev.agent-up.installer"));
        Assert.That(plist, Does.Contain("AgentUp.InstallerApp"));
        Assert.That(plist, Does.Contain("CFBundleIconFile"));
        Assert.That(plist, Does.Contain("Agent-Up.png"));
        Assert.That(plist, Does.Contain("1.2.3"));
    }

    [Test]
    public void LaunchDaemonPlist_containsServiceContract()
    {
        var manifest = MacOsPackageManifest.From(new PackageRequest("/repo", "macos", "osx-arm64", "1.2.3", "artifacts", "Release"));

        var plist = new MacOsPlistGenerator(manifest).LaunchDaemonPlist();

        Assert.That(plist, Does.Contain("dev.agent-up.server"));
        Assert.That(plist, Does.Contain("/Library/Application Support/Agent-Up/server/AgentUp.Server"));
        Assert.That(plist, Does.Contain("http://127.0.0.1:5000"));
        Assert.That(plist, Does.Contain("DOTNET_BUNDLE_EXTRACT_BASE_DIR"));
        Assert.That(plist, Does.Contain("/Library/Application Support/Agent-Up/bundle-cache"));
        Assert.That(plist, Does.Contain("/Library/Logs/Agent-Up/server.out.log"));
        Assert.That(plist, Does.Contain("<integer>5</integer>"));
    }
}
