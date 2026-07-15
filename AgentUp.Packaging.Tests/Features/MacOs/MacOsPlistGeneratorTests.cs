using AgentUp.Packaging.Features.MacOs;
using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Tests.Features.MacOs;

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
        Assert.That(plist, Does.Contain("CFBundleShortVersionString"));
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
