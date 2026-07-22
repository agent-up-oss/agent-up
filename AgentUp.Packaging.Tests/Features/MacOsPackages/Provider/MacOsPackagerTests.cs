using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.MacOsPackages.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

namespace AgentUp.Packaging.Tests.Features.MacOsPackages.Provider;

[TestFixture]
public class MacOsPackagerTests
{
    [Test]
    public async Task PackageAsync_invokesPkgbuildForComponentsAndProductbuildForFinalPkg()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingMacOsPackageWriter();
        var packageTool = new RecordingMacOsPackageTool();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-MacOsPackagerTests", Guid.NewGuid().ToString());
        var request = new PackageRequest(root, "macos", "osx-arm64", "1.2.3", "out", "Release");

        try
        {
            await new MacOsPackager(writer, CreatePayloads(commands, writer), packageTool).PackageAsync(request);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        Assert.That(commands.Commands.Count(command => command.FileName == "dotnet" && command.Arguments.Contains("publish")), Is.EqualTo(4));
        Assert.That(packageTool.ComponentBuilds.Single().Request, Is.EqualTo(request));
        Assert.That(packageTool.ProductBuilds.Single().ProductPackagePath, Is.EqualTo(Path.Join(root, "out", "agent-up-macos-osx-arm64.pkg")));
    }

    [Test]
    public async Task PackageAsync_withPayloadRootCopiesPayloadAndSkipsDotNetPublish()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingMacOsPackageWriter();
        var packageTool = new RecordingMacOsPackageTool();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-MacOsPackagerTests", Guid.NewGuid().ToString());
        var payloadRoot = Path.Join(root, "payload");
        var request = new PackageRequest(root, "macos", "osx-arm64", "1.2.3", "out", "Release", payloadRoot);

        try
        {
            WritePayloadFile(payloadRoot, "installer", "AgentUp.InstallerApp");
            WritePayloadFile(payloadRoot, "desktop", "AgentUp.Desktop");
            WritePayloadFile(payloadRoot, "server", "AgentUp.Server");
            WritePayloadFile(payloadRoot, "cli", "AgentUp.CLI");

            await new MacOsPackager(writer, CreatePayloads(commands, writer), packageTool).PackageAsync(request);

            Assert.That(commands.Commands.Any(command => command.FileName == "dotnet"), Is.False);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "macos-osx-arm64", "installer", "AgentUp.InstallerApp")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "macos-osx-arm64", "desktop", "AgentUp.Desktop")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "macos-osx-arm64", "server", "AgentUp.Server")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "macos-osx-arm64", "cli", "AgentUp.CLI")), Is.True);
            Assert.That(packageTool.ComponentBuilds, Has.Count.EqualTo(1));
            Assert.That(packageTool.ProductBuilds, Has.Count.EqualTo(1));
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

    private static PayloadStagingController CreatePayloads(ICommandRunner commands, IMacOsPackageWriter writer)
        => new(new PackagePayloadStager(new PackagePublisher(commands), writer));

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<CommandSpec> Commands { get; } = [];

        public Task<CommandResult> RunAsync(CommandSpec command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            if (command.FileName == "dotnet" && command.Arguments.Contains("publish"))
            {
                var outputIndex = -1;
                for (var i = 0; i < command.Arguments.Count; i++)
                {
                    if (command.Arguments[i] == "-o")
                    {
                        outputIndex = i;
                        break;
                    }
                }

                if (outputIndex >= 0 && outputIndex + 1 < command.Arguments.Count)
                    Directory.CreateDirectory(command.Arguments[outputIndex + 1]);
            }

            return Task.FromResult(new CommandResult(0, "", ""));
        }
    }

    private sealed class RecordingMacOsPackageWriter : IMacOsPackageWriter
    {
        public void ResetDirectory(string path) { }
        public void CreateDirectory(string path) { }
        public void CopyDirectory(string source, string destination) { }
        public void CopyFile(string source, string destination) { }
        public void WriteText(string path, string text) { }
        public void SetExecutable(string path) { }
    }

    private sealed class RecordingMacOsPackageTool : IMacOsPackageTool
    {
        public List<(PackageRequest Request, MacOsPackageLayout Layout)> ComponentBuilds { get; } = [];
        public List<MacOsPackageLayout> ProductBuilds { get; } = [];

        public Task BuildComponentPackagesAsync(PackageRequest request, MacOsPackageLayout layout, CancellationToken cancellationToken = default)
        {
            ComponentBuilds.Add((request, layout));
            return Task.CompletedTask;
        }

        public Task BuildProductPackageAsync(MacOsPackageLayout layout, CancellationToken cancellationToken = default)
        {
            ProductBuilds.Add(layout);
            return Task.CompletedTask;
        }
    }
}
