using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts.Provider;

[TestFixture]
public class PackagePayloadStagerTests
{
    [Test]
    public async Task StageAsync_withoutPayloadRootPublishesInstallerDesktopServerAndCli()
    {
        var commands = new RecordingCommandRunner();
        var files = new RecordingPackageFileSystem();
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "out", "Release");

        await new PackagePayloadStager(new PackagePublisher(commands), files).StageAsync(new PayloadStagingRequest(
            request,
            "/stage/installer",
            "/stage/desktop",
            "/stage/server",
            "/stage/cli"));

        Assert.That(files.ResetDirectories, Is.EqualTo(new[] { "/repo/artifacts/stage/ubuntu-linux-x64" }));
        Assert.That(files.CreatedDirectories, Is.EqualTo(new[] { "/repo/out" }));
        Assert.That(commands.Commands.Count(command => command.FileName == "dotnet" && command.Arguments.Contains("publish")), Is.EqualTo(4));
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("/repo/AgentUp.InstallerApp/AgentUp.InstallerApp.csproj")), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("/repo/AgentUp.Desktop/AgentUp.Desktop.csproj")), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("/repo/AgentUp.Server/AgentUp.Server.csproj")), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("/repo/AgentUp.CLI/AgentUp.CLI.csproj")), Is.True);
    }

    [Test]
    public async Task StageAsync_withPayloadRootCopiesPrebuiltPayloadAndSkipsPublish()
    {
        var commands = new RecordingCommandRunner();
        var files = new RecordingPackageFileSystem();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-PackagePayloadStagerTests", Guid.NewGuid().ToString());
        var payloadRoot = Path.Join(root, "payload");
        var request = new PackageRequest(root, "windows", "win-x64", "1.2.3", "out", "Release", payloadRoot);

        try
        {
            WritePayloadFile(payloadRoot, "installer", "AgentUp.InstallerApp");
            WritePayloadFile(payloadRoot, "desktop", "AgentUp.Desktop");
            WritePayloadFile(payloadRoot, "server", "AgentUp.Server");
            WritePayloadFile(payloadRoot, "cli", "AgentUp.CLI");

            await new PackagePayloadStager(new PackagePublisher(commands), files).StageAsync(new PayloadStagingRequest(
                request,
                Path.Join(root, "stage", "installer"),
                Path.Join(root, "stage", "desktop"),
                Path.Join(root, "stage", "server"),
                Path.Join(root, "stage", "cli")));

            Assert.That(commands.Commands.Any(command => command.FileName == "dotnet"), Is.False);
            Assert.That(File.Exists(Path.Join(root, "stage", "installer", "AgentUp.InstallerApp")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "stage", "desktop", "AgentUp.Desktop")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "stage", "server", "AgentUp.Server")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "stage", "cli", "AgentUp.CLI")), Is.True);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
}
