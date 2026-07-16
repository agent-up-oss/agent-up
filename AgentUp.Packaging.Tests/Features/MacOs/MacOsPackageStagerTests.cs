using AgentUp.Packaging.Features.MacOs;
using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Tests.Features.MacOs;

[TestFixture]
public class MacOsPackageStagerTests
{
    [Test]
    public void Stage_materializesAppBundleComponentRootsScriptsAndDistribution()
    {
        var request = new PackageRequest("/repo", "macos", "osx-arm64", "1.2.3", "artifacts", "Release");
        var layout = MacOsPackageLayout.From(request);
        var manifest = MacOsPackageManifest.From(request);
        var writer = new RecordingMacOsPackageWriter();

        new MacOsPackageStager(writer).Stage(layout, manifest);

        Assert.That(writer.CopiedDirectories, Does.Contain((layout.InstallerPublishDirectory, layout.InstallerAppMacOsDirectory)));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.DesktopPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "desktop"))));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.ServerPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "server"))));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.CliPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "cli"))));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.DesktopPublishDirectory, layout.DesktopAppMacOsDirectory)));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.DesktopPublishDirectory, Path.Join(layout.DesktopComponentRoot, "usr", "local", "agent-up", "desktop"))));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.CliPublishDirectory, Path.Join(layout.CliComponentRoot, "usr", "local", "agent-up", "cli"))));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.ServerPublishDirectory, Path.Join(layout.ServerComponentRoot, "Library", "Application Support", "Agent-Up", "server"))));
        Assert.That(writer.WrittenText[layout.InstallerInfoPlistPath], Does.Contain("AgentUp.InstallerApp"));
        Assert.That(writer.WrittenText[layout.DesktopInfoPlistPath], Does.Contain("AgentUp.Desktop"));
        Assert.That(writer.WrittenText[layout.LaunchDaemonPlistPath], Does.Contain("dev.agent-up.server"));
        Assert.That(writer.WrittenText[layout.PostInstallScriptPath], Does.Contain("launchctl bootstrap system"));
        Assert.That(writer.WrittenText[layout.PostInstallScriptPath], Does.Contain("open -a \"/Applications/Agent-Up Installer.app\""));
        Assert.That(writer.WrittenText[layout.DistributionXmlPath], Does.Contain("InstallerApp.pkg"));
        Assert.That(writer.WrittenText[layout.DistributionXmlPath], Does.Contain("DesktopApp.pkg"));
        Assert.That(writer.ExecutablePaths, Does.Contain(layout.PostInstallScriptPath));
    }

    private sealed class RecordingMacOsPackageWriter : IMacOsPackageWriter
    {
        public List<(string Source, string Destination)> CopiedDirectories { get; } = [];
        public Dictionary<string, string> WrittenText { get; } = [];
        public List<string> ExecutablePaths { get; } = [];

        public void ResetDirectory(string path) { }
        public void CreateDirectory(string path) { }
        public void CopyDirectory(string source, string destination) => CopiedDirectories.Add((source, destination));
        public void CopyFile(string source, string destination) { }
        public void WriteText(string path, string text) => WrittenText[path] = text;
        public void SetExecutable(string path) => ExecutablePaths.Add(path);
    }
}
