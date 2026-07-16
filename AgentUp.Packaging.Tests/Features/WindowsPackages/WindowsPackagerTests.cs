using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;
using AgentUp.Packaging.Features.WindowsPackages;
using AgentUp.Packaging.Features.WindowsPackages.Providers;
using AgentUp.Packaging.Features.WindowsPackages.Services;

namespace AgentUp.Packaging.Tests.Features.WindowsPackages;

[TestFixture]
public class WindowsPackagerTests
{
    [Test]
    public async Task PackageAsync_publishesComponentsGeneratesWixAndInvokesWixBuilds()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingWindowsPackageWriter();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-WindowsPackagerTests", Guid.NewGuid().ToString());
        var request = new PackageRequest(root, "windows", "win-x64", "1.2.3", "out", "Release");

        try
        {
            await new WindowsPackager(commands, writer).PackageAsync(request);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        Assert.That(commands.Commands.Count(command => command.FileName == "dotnet" && command.Arguments.Contains("publish")), Is.EqualTo(3));
        Assert.That(commands.Commands.Count(command => command.FileName == "wix"), Is.EqualTo(3));
        Assert.That(commands.Commands[^3].Arguments, Is.EqualTo(new[] { "eula", "accept", "wix7" }));
        Assert.That(commands.Commands[^2].Arguments.Any(argument => argument.EndsWith("Product.wxs", StringComparison.Ordinal)), Is.True);
        Assert.That(commands.Commands[^2].Arguments.Any(argument => argument.EndsWith("Product.msi", StringComparison.Ordinal)), Is.True);
        Assert.That(commands.Commands[^1].Arguments.Any(argument => argument.EndsWith("Bundle.wxs", StringComparison.Ordinal)), Is.True);
        Assert.That(commands.Commands[^1].Arguments, Does.Contain("WixToolset.Bal.wixext"));
        Assert.That(commands.Commands[^1].Arguments, Does.Contain(Path.Join(root, "out", "agent-up-windows-win-x64.exe")));
        Assert.That(writer.CopiedFiles, Does.Contain((Path.Join(root, "artifacts", "stage", "windows-win-x64", "Product.msi"), Path.Join(root, "out", "agent-up-windows-win-x64.msi"))));
        Assert.That(writer.WrittenText.Keys.Any(path => path.EndsWith("Product.wxs", StringComparison.Ordinal)), Is.True);
        Assert.That(writer.WrittenText.Keys.Any(path => path.EndsWith("Bundle.wxs", StringComparison.Ordinal)), Is.True);
        Assert.That(writer.WrittenText.Keys.Any(path => path.EndsWith("agent-up.cmd", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public async Task PackageAsync_withPayloadRootCopiesPayloadAndSkipsDotNetPublish()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingWindowsPackageWriter();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-WindowsPackagerTests", Guid.NewGuid().ToString());
        var payloadRoot = Path.Join(root, "payload");
        var request = new PackageRequest(root, "windows", "win-x64", "1.2.3", "out", "Release", payloadRoot);

        try
        {
            WritePayloadFile(payloadRoot, "desktop", "AgentUp.Desktop.exe");
            WritePayloadFile(payloadRoot, "server", "AgentUp.Server.exe");
            WritePayloadFile(payloadRoot, "cli", "AgentUp.CLI.exe");

            await new WindowsPackager(commands, writer).PackageAsync(request);

            Assert.That(commands.Commands.Any(command => command.FileName == "dotnet"), Is.False);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "windows-win-x64", "desktop", "AgentUp.Desktop.exe")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "windows-win-x64", "server", "AgentUp.Server.exe")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "windows-win-x64", "cli", "AgentUp.CLI.exe")), Is.True);
            Assert.That(commands.Commands.Count(command => command.FileName == "wix"), Is.EqualTo(3));
            Assert.That(writer.CopiedFiles, Does.Contain((Path.Join(root, "artifacts", "stage", "windows-win-x64", "Product.msi"), Path.Join(root, "out", "agent-up-windows-win-x64.msi"))));
            Assert.That(writer.WrittenText.Keys.Any(path => path.EndsWith("Product.wxs", StringComparison.Ordinal)), Is.True);
            Assert.That(writer.WrittenText.Keys.Any(path => path.EndsWith("Bundle.wxs", StringComparison.Ordinal)), Is.True);
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
                {
                    var output = command.Arguments[outputIndex + 1];
                    Directory.CreateDirectory(output);
                    var project = command.Arguments[1];
                    var fileName = project.Contains("Desktop", StringComparison.Ordinal)
                        ? "AgentUp.Desktop.exe"
                        : project.Contains("Server", StringComparison.Ordinal)
                            ? "AgentUp.Server.exe"
                            : "AgentUp.CLI.exe";
                    File.WriteAllText(Path.Join(output, fileName), "");
                }
            }

            return Task.FromResult(new CommandResult(0, "", ""));
        }
    }

    private sealed class RecordingWindowsPackageWriter : IWindowsPackageWriter
    {
        public Dictionary<string, string> WrittenText { get; } = [];
        public List<(string SourcePath, string DestinationPath)> CopiedFiles { get; } = [];

        public void ResetDirectory(string path) { }
        public void CreateDirectory(string path) { }
        public void WriteText(string path, string text) => WrittenText[path] = text;
        public void CopyFile(string sourcePath, string destinationPath) => CopiedFiles.Add((sourcePath, destinationPath));
    }
}
