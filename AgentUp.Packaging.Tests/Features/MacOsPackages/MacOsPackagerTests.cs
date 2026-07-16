using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages;
using AgentUp.Packaging.Features.MacOsPackages.Providers;
using AgentUp.Packaging.Features.MacOsPackages.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

namespace AgentUp.Packaging.Tests.Features.MacOsPackages;

[TestFixture]
public class MacOsPackagerTests
{
    [Test]
    public async Task PackageAsync_invokesPkgbuildForComponentsAndProductbuildForFinalPkg()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingMacOsPackageWriter();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-MacOsPackagerTests", Guid.NewGuid().ToString());
        var request = new PackageRequest(root, "macos", "osx-arm64", "1.2.3", "out", "Release");

        try
        {
            await new MacOsPackager(commands, writer).PackageAsync(request);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        Assert.That(commands.Commands.Count(command => command.FileName == "dotnet" && command.Arguments.Contains("publish")), Is.EqualTo(3));
        Assert.That(commands.Commands.Count(command => command.FileName == "pkgbuild"), Is.EqualTo(3));
        Assert.That(commands.Commands.Last().FileName, Is.EqualTo("productbuild"));
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.desktop")), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.cli")), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.server")), Is.True);
        Assert.That(commands.Commands.Last().Arguments, Does.Contain(Path.Join(root, "out", "agent-up-macos-osx-arm64.pkg")));
    }

    [Test]
    public async Task PackageAsync_withPayloadRootCopiesPayloadAndSkipsDotNetPublish()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingMacOsPackageWriter();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-MacOsPackagerTests", Guid.NewGuid().ToString());
        var payloadRoot = Path.Join(root, "payload");
        var request = new PackageRequest(root, "macos", "osx-arm64", "1.2.3", "out", "Release", payloadRoot);

        try
        {
            WritePayloadFile(payloadRoot, "desktop", "AgentUp.Desktop");
            WritePayloadFile(payloadRoot, "server", "AgentUp.Server");
            WritePayloadFile(payloadRoot, "cli", "AgentUp.CLI");

            await new MacOsPackager(commands, writer).PackageAsync(request);

            Assert.That(commands.Commands.Any(command => command.FileName == "dotnet"), Is.False);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "macos-osx-arm64", "desktop", "AgentUp.Desktop")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "macos-osx-arm64", "server", "AgentUp.Server")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "macos-osx-arm64", "cli", "AgentUp.CLI")), Is.True);
            Assert.That(commands.Commands.Count(command => command.FileName == "pkgbuild"), Is.EqualTo(3));
            Assert.That(commands.Commands.Last().FileName, Is.EqualTo("productbuild"));
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
}
