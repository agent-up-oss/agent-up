using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Features.UbuntuPackages.Services;

namespace AgentUp.Packaging.Tests.Features.UbuntuPackages.Provider;

[TestFixture]
public class UbuntuPackagerTests
{
    [Test]
    public async Task PackageAsync_invokesDotNetPublishesAndDpkgBuild()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingPackageWriter();
        var packageTool = new RecordingUbuntuPackageTool();
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "out", "Release");

        await new UbuntuPackager(writer, CreatePayloads(commands, writer), packageTool).PackageAsync(request);

        var publishCommands = commands.Commands
            .Where(command => command.FileName == "dotnet" && command.Arguments.Contains("publish"))
            .ToList();

        Assert.That(publishCommands, Has.Count.EqualTo(3));
        Assert.That(publishCommands, Has.All.Matches<CommandSpec>(command => command.Arguments.Contains("-p:PublishSingleFile=true")));
        Assert.That(publishCommands, Has.All.Matches<CommandSpec>(command => command.Arguments.Contains("-p:IncludeNativeLibrariesForSelfExtract=true")));
        Assert.That(publishCommands, Has.All.Matches<CommandSpec>(command => command.Arguments.Contains("-p:IncludeAllContentForSelfExtract=true")));
        Assert.That(packageTool.Layouts.Single().DebOutputPath, Is.EqualTo(Path.Join("/repo", "out", "agent-up-ubuntu-linux-x64.deb")));
        Assert.That(writer.CreatedDirectories, Does.Contain(Path.Join("/repo", "out")));
    }

    [Test]
    public async Task PackageAsync_withPayloadRootCopiesPayloadAndSkipsDotNetPublish()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingPackageWriter();
        var packageTool = new RecordingUbuntuPackageTool();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-UbuntuPackagerTests", Guid.NewGuid().ToString());
        var payloadRoot = Path.Join(root, "payload");
        var request = new PackageRequest(root, "ubuntu", "linux-x64", "1.2.3", "out", "Release", payloadRoot);

        try
        {
            WritePayloadFile(payloadRoot, "desktop", "AgentUp.Desktop");
            WritePayloadFile(payloadRoot, "server", "AgentUp.Server");
            WritePayloadFile(payloadRoot, "cli", "AgentUp.CLI");

            await new UbuntuPackager(writer, CreatePayloads(commands, writer), packageTool).PackageAsync(request);

            Assert.That(commands.Commands.Any(command => command.FileName == "dotnet"), Is.False);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "ubuntu-linux-x64", "desktop", "AgentUp.Desktop")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "ubuntu-linux-x64", "server", "AgentUp.Server")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "ubuntu-linux-x64", "cli", "AgentUp.CLI")), Is.True);
            Assert.That(packageTool.Layouts, Has.Count.EqualTo(1));
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

    private static PayloadStagingController CreatePayloads(ICommandRunner commands, IPackageWriter writer)
        => new(new PackagePayloadStager(new PackagePublisher(commands), writer));

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<CommandSpec> Commands { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(new CommandResult(0, "", ""));
        }
    }

    private sealed class RecordingPackageWriter : IPackageWriter
    {
        public List<string> CreatedDirectories { get; } = [];

        public void ResetDirectory(string path) => CreatedDirectories.Add(path);
        public void CreateDirectory(string path) => CreatedDirectories.Add(path);
        public void CopyDirectory(string source, string destination) { }
        public void CopyFile(string source, string destination) { }
        public void WriteText(string path, string text) { }
        public void CreateSymbolicLink(string linkPath, string targetPath) { }
        public void SetExecutable(string path) { }
    }

    private sealed class RecordingUbuntuPackageTool : IUbuntuPackageTool
    {
        public List<UbuntuPackageLayout> Layouts { get; } = [];

        public Task BuildDebAsync(UbuntuPackageLayout layout, CancellationToken cancellationToken = default)
        {
            Layouts.Add(layout);
            return Task.CompletedTask;
        }
    }
}
