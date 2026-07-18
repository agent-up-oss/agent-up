using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.MacOsPackages.Providers;
using AgentUp.Packaging.Features.MacOsPackages.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Tests.Features.MacOsPackages;

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
        Assert.That(writer.CreatedDirectories, Does.Contain(layout.InstallerAppResourcesDirectory));
        Assert.That(writer.CreatedDirectories, Does.Contain(layout.InstallerPayloadIconDirectory));
        Assert.That(writer.CopiedFiles, Does.Contain((Path.Join(request.RepositoryRoot, "media", "logo.png"), layout.InstallerIconPath)));
        Assert.That(writer.CopiedFiles, Does.Contain((Path.Join(request.RepositoryRoot, "media", "logo.png"), layout.InstallerPayloadIconPath)));
        Assert.That(writer.CopiedDirectories.Any(copy => copy.Destination.Contains("Agent-Up.app")), Is.False);
        Assert.That(writer.CopiedDirectories.Any(copy => copy.Destination.Contains("usr/local/agent-up")), Is.False);
        Assert.That(writer.CopiedDirectories.Any(copy => copy.Destination.Contains("Library/Application Support/Agent-Up")), Is.False);
        Assert.That(writer.WrittenText[layout.InstallerInfoPlistPath], Does.Contain("AgentUp.InstallerApp"));
        var installerPreInstallScriptPath = Path.Join(layout.InstallerScriptsDirectory, "preinstall");
        var installerPostInstallScriptPath = Path.Join(layout.InstallerScriptsDirectory, "postinstall");
        Assert.That(writer.WrittenText[installerPreInstallScriptPath], Does.Contain("rm -rf \"/Applications/Agent-Up Installer.app\""));
        Assert.That(writer.WrittenText[installerPostInstallScriptPath], Does.Contain("open -a \"/Applications/Agent-Up Installer.app\""));
        Assert.That(writer.WrittenText[layout.DistributionXmlPath], Does.Contain("InstallerApp.pkg"));
        Assert.That(writer.WrittenText[layout.DistributionXmlPath], Does.Not.Contain("DesktopApp.pkg"));
        Assert.That(writer.WrittenText[layout.DistributionXmlPath], Does.Not.Contain("Server.pkg"));
        Assert.That(writer.WrittenText[layout.DistributionXmlPath], Does.Not.Contain("CLI.pkg"));
        Assert.That(writer.ExecutablePaths, Does.Contain(installerPreInstallScriptPath));
        Assert.That(writer.ExecutablePaths, Does.Contain(installerPostInstallScriptPath));
    }

    private sealed class RecordingMacOsPackageWriter : IMacOsPackageWriter
    {
        public List<string> CreatedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedFiles { get; } = [];
        public Dictionary<string, string> WrittenText { get; } = [];
        public List<string> ExecutablePaths { get; } = [];

        public void ResetDirectory(string path) { }
        public void CreateDirectory(string path) => CreatedDirectories.Add(path);
        public void CopyDirectory(string source, string destination) => CopiedDirectories.Add((source, destination));
        public void CopyFile(string source, string destination) => CopiedFiles.Add((source, destination));
        public void WriteText(string path, string text) => WrittenText[path] = text;
        public void SetExecutable(string path) => ExecutablePaths.Add(path);
    }
}
