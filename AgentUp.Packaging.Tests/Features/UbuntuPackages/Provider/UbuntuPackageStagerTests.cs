using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Features.UbuntuPackages.Providers;
using AgentUp.Packaging.Features.UbuntuPackages.Services;

namespace AgentUp.Packaging.Tests.Features.UbuntuPackages.Provider;

[TestFixture]
public class UbuntuPackageStagerTests
{
    [Test]
    public void Stage_materializesExpectedDebianLayout()
    {
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release");
        var layout = UbuntuPackageLayout.From(request);
        var manifest = UbuntuPackageManifest.From(request);
        var writer = new RecordingPackageWriter();

        new UbuntuPackageStager(writer).Stage(request, layout, manifest);

        Assert.That(writer.CreatedDirectories, Does.Contain(Path.Join(layout.DebRoot, "DEBIAN")));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.CliPublishDirectory, Path.Join(layout.DebRoot, "opt", "agent-up", "cli"))));
        Assert.That(writer.CopiedFiles, Does.Contain((Path.Join("/repo", "packaging", "linux", "agent-up-server.service"), Path.Join(layout.DebRoot, "etc", "systemd", "system", "agent-up-server.service"))));
        Assert.That(writer.Symlinks, Does.Contain((Path.Join(layout.DebRoot, "usr", "bin", "agent-up"), "/opt/agent-up/cli/AgentUp.CLI")));
        Assert.That(writer.WrittenText[Path.Join(layout.DebRoot, "usr", "share", "applications", "agent-up.desktop")], Does.Contain("Exec=/opt/agent-up/desktop/AgentUp.Desktop"));
        Assert.That(writer.ExecutablePaths, Does.Contain(Path.Join(layout.DebRoot, "DEBIAN", "postinst")));
    }

    private sealed class RecordingPackageWriter : IPackageWriter
    {
        public List<string> CreatedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedFiles { get; } = [];
        public List<(string LinkPath, string TargetPath)> Symlinks { get; } = [];
        public Dictionary<string, string> WrittenText { get; } = [];
        public List<string> ExecutablePaths { get; } = [];

        public void ResetDirectory(string path) => CreatedDirectories.Add(path);
        public void CreateDirectory(string path) => CreatedDirectories.Add(path);
        public void CopyDirectory(string source, string destination) => CopiedDirectories.Add((source, destination));
        public void CopyFile(string source, string destination) => CopiedFiles.Add((source, destination));
        public void WriteText(string path, string text) => WrittenText[path] = text;
        public void CreateSymbolicLink(string linkPath, string targetPath) => Symlinks.Add((linkPath, targetPath));
        public void SetExecutable(string path) => ExecutablePaths.Add(path);
    }
}
