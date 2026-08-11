using LocalInstaller.Core.Shared.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Interfaces;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Providers;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Services;
using LocalInstaller.Packaging.Shared.Interfaces;
using LocalInstaller.Packaging.Tests.Support;

namespace LocalInstaller.Packaging.Tests.Features.ReleaseArtifacts.Provider;

[TestFixture]
public class PackagePayloadStagerTests
{
    private static readonly string Root = Path.GetFullPath(Path.Join(Path.GetTempPath(), "pkg"));
    [Test]
    public async Task StageAsync_withoutPayloadRootPublishesInstallerDesktopServerCliAndTray()
    {
        var commands = new RecordingCommandRunner();
        var files = new RecordingPackageFileSystem();
        var request = new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "out", "Release", AgentUpPackageTestManifests.Product());

        await new PackagePayloadStager(new PackagePublisher(commands), files).StageAsync(new PayloadStagingRequest(
            request,
            "/stage/installer",
            "/stage/desktop",
            "/stage/server",
            "/stage/cli",
            "/stage/tray"));

        Assert.That(files.ResetDirectories, Is.EqualTo(new[] { Path.Join(Root, "artifacts", "stage", "ubuntu-linux-x64") }));
        Assert.That(files.CreatedDirectories, Is.EqualTo(new[] { Path.Join(Root, "out") }));
        Assert.That(commands.Commands.Count(command => command.FileName == "dotnet" && command.Arguments.Contains("publish")), Is.EqualTo(5));
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains(Path.Join(Root, "AgentUp.InstallerApp", "AgentUp.InstallerApp.csproj"))), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains(Path.Join(Root, "AgentUp.Desktop", "AgentUp.Desktop.csproj"))), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains(Path.Join(Root, "AgentUp.Server", "AgentUp.Server.csproj"))), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains(Path.Join(Root, "AgentUp.CLI", "AgentUp.CLI.csproj"))), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains(Path.Join(Root, "AgentUp.Tray", "AgentUp.Tray.csproj"))), Is.True);
    }

    [Test]
    public async Task StageAsync_withPayloadRootCopiesPrebuiltPayloadAndSkipsPublish()
    {
        var commands = new RecordingCommandRunner();
        var files = new RecordingPackageFileSystem();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-PackagePayloadStagerTests", Guid.NewGuid().ToString());
        var payloadRoot = Path.Join(root, "payload");
        var request = new PackageRequest(root, "windows", "win-x64", "1.2.3", "out", "Release", payloadRoot, AgentUpPackageTestManifests.Product());

        try
        {
            WritePayloadFile(payloadRoot, "installer", "AgentUp.InstallerApp");
            WritePayloadFile(payloadRoot, "desktop", "AgentUp.Desktop");
            WritePayloadFile(payloadRoot, "server", "AgentUp.Server");
            WritePayloadFile(payloadRoot, "cli", "AgentUp.CLI");
            WritePayloadFile(payloadRoot, "tray", "AgentUp.Tray");

            await new PackagePayloadStager(new PackagePublisher(commands), files).StageAsync(new PayloadStagingRequest(
                request,
                Path.Join(root, "stage", "installer"),
                Path.Join(root, "stage", "desktop"),
                Path.Join(root, "stage", "server"),
                Path.Join(root, "stage", "cli"),
                Path.Join(root, "stage", "tray")));

            Assert.That(commands.Commands.Any(command => command.FileName == "dotnet"), Is.False);
            Assert.That(File.Exists(Path.Join(root, "stage", "installer", "AgentUp.InstallerApp")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "stage", "desktop", "AgentUp.Desktop")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "stage", "server", "AgentUp.Server")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "stage", "cli", "AgentUp.CLI")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "stage", "tray", "AgentUp.Tray")), Is.True);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task StageAsync_withManifestOptionsPublishesFlatPayloadIdsAndMirrorsLegacyTargets()
    {
        var publisher = new RecordingPublisher();
        var files = new RecordingPackageFileSystem();
        var product = new PackageProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK")
        {
            InstallerApplication = new PackageProductArtifact("orbit-installer", "Installer", "", "Orbit.Installer", "Orbit.Installer/Orbit.Installer.csproj", "orbit-installer", LocalInstallerArtifactTarget.InstallerApp),
            InstallerOptions =
            [
                new PackageProductArtifact("orbit-cli-admin", "Admin CLI", "", "Orbit.Admin.Cli", "Orbit.Admin.Cli/Orbit.Admin.Cli.csproj", "orbit-cli-admin", LocalInstallerArtifactTarget.Cli),
                new PackageProductArtifact("orbit-cli-user", "User CLI", "", "Orbit.User.Cli", "Orbit.User.Cli/Orbit.User.Cli.csproj", "orbit-cli-user", LocalInstallerArtifactTarget.Cli),
                new PackageProductArtifact("orbit-server", "Server", "", "Orbit.Server", "Orbit.Server/Orbit.Server.csproj", "orbit-server", LocalInstallerArtifactTarget.Server),
                new PackageProductArtifact("orbit-desktop", "Desktop", "", "Orbit.Desktop", "Orbit.Desktop/Orbit.Desktop.csproj", "orbit-desktop", LocalInstallerArtifactTarget.Desktop),
                new PackageProductArtifact("orbit-tray", "Tray", "", "Orbit.Tray", "Orbit.Tray/Orbit.Tray.csproj", "orbit-tray", LocalInstallerArtifactTarget.Tray)
            ]
        };
        var request = new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "out", "Release", product);

        await new PackagePayloadStager(publisher, files).StageAsync(new PayloadStagingRequest(
            request,
            "/stage/installer",
            "/stage/desktop",
            "/stage/server",
            "/stage/cli",
            "/stage/tray"));

        Assert.That(publisher.Published.Select(p => p.OutputDirectory), Does.Contain(Path.Join(request.StageDirectory, "orbit-cli-admin")));
        Assert.That(publisher.Published.Select(p => p.OutputDirectory), Does.Contain(Path.Join(request.StageDirectory, "orbit-cli-user")));
        Assert.That(publisher.Published.Select(p => p.OutputDirectory), Does.Contain("/stage/installer"));
        Assert.That(publisher.Copied.Select(c => c.Destination), Does.Contain("/stage/cli"));
        Assert.That(publisher.Copied.Any(c => c.Source == Path.Join(request.StageDirectory, "orbit-cli-admin") && c.Destination == "/stage/cli"), Is.True);
    }

    [Test]
    public async Task StageAsync_withManifestOptionsWhosePayloadNamesMatchLegacyTargets_doesNotSelfMirror()
    {
        var publisher = new RecordingPublisher();
        var files = new RecordingPackageFileSystem();
        var product = new PackageProductManifest("Agent-Up", "agent-up", "AGENTUP")
        {
            InstallerApplication = new PackageProductArtifact("agent-up-installer", "Installer", "", "AgentUp.InstallerApp", "AgentUp.InstallerApp/AgentUp.InstallerApp.csproj", "installer", LocalInstallerArtifactTarget.InstallerApp),
            InstallerOptions =
            [
                new PackageProductArtifact("agent-up-cli", "CLI", "", "AgentUp.CLI", "AgentUp.CLI/AgentUp.CLI.csproj", "cli", LocalInstallerArtifactTarget.Cli),
                new PackageProductArtifact("agent-up-server", "Server", "", "AgentUp.Server", "AgentUp.Server/AgentUp.Server.csproj", "server", LocalInstallerArtifactTarget.Server),
                new PackageProductArtifact("agent-up-desktop", "Desktop", "", "AgentUp.Desktop", "AgentUp.Desktop/AgentUp.Desktop.csproj", "desktop", LocalInstallerArtifactTarget.Desktop),
                new PackageProductArtifact("agent-up-tray", "Tray", "", "AgentUp.Tray", "AgentUp.Tray/AgentUp.Tray.csproj", "tray", LocalInstallerArtifactTarget.Tray)
            ]
        };
        var request = new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "out", "Release", product);

        await new PackagePayloadStager(publisher, files).StageAsync(new PayloadStagingRequest(
            request,
            Path.Join(request.StageDirectory, "installer"),
            Path.Join(request.StageDirectory, "desktop"),
            Path.Join(request.StageDirectory, "server"),
            Path.Join(request.StageDirectory, "cli"),
            Path.Join(request.StageDirectory, "tray")));

        Assert.That(publisher.Published.Select(p => p.OutputDirectory), Does.Contain(Path.Join(request.StageDirectory, "desktop")));
        Assert.That(publisher.Published.Select(p => p.OutputDirectory), Does.Contain(Path.Join(request.StageDirectory, "server")));
        Assert.That(publisher.Published.Select(p => p.OutputDirectory), Does.Contain(Path.Join(request.StageDirectory, "cli")));
        Assert.That(publisher.Published.Select(p => p.OutputDirectory), Does.Contain(Path.Join(request.StageDirectory, "tray")));
        Assert.That(publisher.Copied.Any(c => Path.GetFullPath(c.Source).Equals(Path.GetFullPath(c.Destination), StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    private static void WritePayloadFile(string payloadRoot, string component, string fileName)
    {
        var directory = Path.Join(payloadRoot, component);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, fileName), "");
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<CommandSpec> Commands { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(new CommandResult(0, "", ""));
        }
    }

    private sealed class RecordingPackageFileSystem : IPackageFileSystem
    {
        public List<string> ResetDirectories { get; } = [];
        public List<string> CreatedDirectories { get; } = [];

        public void ResetDirectory(string path) => ResetDirectories.Add(path);
        public void CreateDirectory(string path) => CreatedDirectories.Add(path);
        public void CopyFile(string source, string destination) { }
        public void WriteText(string path, string text) { }
    }

    private sealed class RecordingPublisher : IPackagePublisher
    {
        public List<(string ProjectPath, string OutputDirectory)> Published { get; } = [];
        public List<(string Source, string Destination)> Copied { get; } = [];

        public Task PublishDotNetProjectAsync(
            string projectPath,
            string runtimeId,
            string configuration,
            string version,
            string outputDirectory,
            CancellationToken cancellationToken = default)
        {
            Published.Add((projectPath, outputDirectory));
            return Task.CompletedTask;
        }

        public void CopyPrebuiltPayload(string payloadDirectory, string outputDirectory)
            => Copied.Add((payloadDirectory, outputDirectory));
    }
}
