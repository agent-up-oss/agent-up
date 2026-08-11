using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.UbuntuPackages.Interfaces;
using LocalInstaller.Packaging.Features.UbuntuPackages.Models;
using LocalInstaller.Packaging.Features.UbuntuPackages.Services;
using LocalInstaller.Packaging.Tests.Support;

namespace LocalInstaller.Packaging.Tests.Features.UbuntuPackages.Provider;

[TestFixture]
public class UbuntuPackageStagerTests
{
    private static readonly string Root = Path.GetFullPath(Path.Join(Path.GetTempPath(), "pkg"));
    [Test]
    public void Stage_materializesExpectedDebianLayout()
    {
        var request = new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "artifacts", "Release", AgentUpPackageTestManifests.Product());
        var layout = UbuntuPackageLayout.From(request);
        var manifest = UbuntuPackageManifest.From(request);
        var writer = new RecordingPackageWriter();

        new UbuntuPackageStager(writer).Stage(request, layout, manifest);

        Assert.That(writer.CreatedDirectories, Does.Contain(Path.Join(layout.DebRoot, "DEBIAN")));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.InstallerPublishDirectory, Path.Join(layout.DebRoot, "opt", "agent-up", "installer"))));
        Assert.That(writer.CopiedDirectories, Does.Contain((layout.CliPublishDirectory, Path.Join(layout.DebRoot, "opt", "agent-up", "installer", "payload", "cli"))));
        Assert.That(writer.CopiedFiles, Does.Contain((Path.Join(Root, "packaging", "linux", "agent-up-server.service"), Path.Join(layout.DebRoot, "opt", "agent-up", "installer", "payload", "service", "agent-up-server.service"))));
        Assert.That(writer.ExecutablePaths, Does.Contain(Path.Join(layout.DebRoot, "opt", "agent-up", "installer", "AgentUp.InstallerApp")));
        Assert.That(writer.WrittenText[Path.Join(layout.DebRoot, "DEBIAN", "postinst")], Does.Contain("AgentUp.InstallerApp"));
        Assert.That(writer.WrittenText[Path.Join(layout.DebRoot, "DEBIAN", "postinst")], Does.Not.Contain("--install-core"));
        Assert.That(writer.WrittenText, Contains.Key(Path.Join(layout.DebRoot, "usr", "share", "applications", "agent-up-installer.desktop")));
        Assert.That(writer.WrittenText, Contains.Key(Path.Join(layout.DebRoot, "usr", "share", "metainfo", "agent-up-installer.desktop.metainfo.xml")));
        Assert.That(writer.WrittenText[Path.Join(layout.DebRoot, "usr", "share", "metainfo", "agent-up-installer.desktop.metainfo.xml")], Does.Contain("<pkgname>agent-up</pkgname>"));
        Assert.That(writer.WrittenText[Path.Join(layout.DebRoot, "usr", "share", "metainfo", "agent-up-installer.desktop.metainfo.xml")], Does.Contain("<release version=\"1.2.3\""));
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
