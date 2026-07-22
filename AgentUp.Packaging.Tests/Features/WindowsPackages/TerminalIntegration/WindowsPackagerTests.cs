using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Services;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;
using AgentUp.Packaging.Features.WindowsPackages.Models;
using AgentUp.Packaging.Features.WindowsPackages.Services;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Packaging.Tests.Features.WindowsPackages.TerminalIntegration;

[TestFixture]
public class WindowsPackagerTests
{
    [Test]
    public async Task PackageAsync_publishesComponentsGeneratesWixAndInvokesWixBuilds()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingWindowsPackageWriter();
        var packagingTool = new RecordingWindowsPackagingTool();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-WindowsPackagerTests", Guid.NewGuid().ToString());
        var request = new PackageRequest(root, "windows", "win-x64", "1.2.3", "out", "Release");

        try
        {
            await new WindowsPackager(writer, CreatePayloads(commands, writer), packagingTool).PackageAsync(request);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        Assert.That(commands.Commands.Count(command => command.FileName == "dotnet" && command.Arguments.Contains("publish")), Is.EqualTo(4));
        Assert.That(packagingTool.Calls.Select(call => call.Name), Is.EqualTo(new[] { "accept", "product", "bundle" }));
        Assert.That(packagingTool.Calls[^1].Request, Is.EqualTo(request));
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
        var packagingTool = new RecordingWindowsPackagingTool();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-WindowsPackagerTests", Guid.NewGuid().ToString());
        var payloadRoot = Path.Join(root, "payload");
        var request = new PackageRequest(root, "windows", "win-x64", "1.2.3", "out", "Release", payloadRoot);

        try
        {
            WritePayloadFile(payloadRoot, "desktop", "AgentUp.Desktop.exe");
            WritePayloadFile(payloadRoot, "server", "AgentUp.Server.exe");
            WritePayloadFile(payloadRoot, "cli", "AgentUp.CLI.exe");
            WritePayloadFile(payloadRoot, "installer", "AgentUp.InstallerApp.exe");

            await new WindowsPackager(writer, CreatePayloads(commands, writer), packagingTool).PackageAsync(request);

            Assert.That(commands.Commands.Any(command => command.FileName == "dotnet"), Is.False);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "windows-win-x64", "installer", "AgentUp.InstallerApp.exe")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "windows-win-x64", "desktop", "AgentUp.Desktop.exe")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "windows-win-x64", "server", "AgentUp.Server.exe")), Is.True);
            Assert.That(File.Exists(Path.Join(root, "artifacts", "stage", "windows-win-x64", "cli", "AgentUp.CLI.exe")), Is.True);
            Assert.That(packagingTool.Calls.Select(call => call.Name), Is.EqualTo(new[] { "accept", "product", "bundle" }));
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

    [Test]
    public async Task PackageAsync_forNonAgentUpProductWritesProductCliShimAndProductMsiArtifact()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingWindowsPackageWriter();
        var packagingTool = new RecordingWindowsPackagingTool();
        var root = Path.Join(Path.GetTempPath(), "AgentUp-WindowsPackagerTests", Guid.NewGuid().ToString());
        var request = new PackageRequest(
            root,
            "windows",
            "win-x64",
            "1.2.3",
            "out",
            "Release",
            productManifest: new ProductManifest("Orbit Desk", "orbit-desk", "ORBITDESK"));

        try
        {
            await new WindowsPackager(writer, CreatePayloads(commands, writer), packagingTool).PackageAsync(request);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        Assert.That(writer.WrittenText.Keys.Any(path => path.EndsWith("orbit-desk.cmd", StringComparison.Ordinal)), Is.True);
        Assert.That(writer.WrittenText.Keys.Any(path => path.EndsWith("agent-up.cmd", StringComparison.Ordinal)), Is.False);
        Assert.That(writer.CopiedFiles, Does.Contain((
            Path.Join(root, "artifacts", "stage", "windows-win-x64", "Product.msi"),
            Path.Join(root, "out", "orbit-desk-windows-win-x64.msi"))));
    }

    private static void WritePayloadFile(string payloadRoot, string component, string fileName)
    {
        var directory = Path.Join(payloadRoot, component);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, fileName), "");
    }

    private static PayloadStagingController CreatePayloads(ICommandRunner commands, IWindowsPackageWriter writer)
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
                {
                    var output = command.Arguments[outputIndex + 1];
                    Directory.CreateDirectory(output);
                    var project = command.Arguments[1];
                    var fileName = project.Contains("InstallerApp", StringComparison.Ordinal)
                        ? "AgentUp.InstallerApp.exe"
                        : project.Contains("Desktop", StringComparison.Ordinal)
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

    private sealed class RecordingWindowsPackagingTool : IWindowsPackagingTool
    {
        public List<(string Name, PackageRequest? Request, WindowsPackageLayout? Layout)> Calls { get; } = [];

        public Task AcceptWixLicenseAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add(("accept", null, null));
            return Task.CompletedTask;
        }

        public Task BuildProductMsiAsync(WindowsPackageLayout layout, CancellationToken cancellationToken = default)
        {
            Calls.Add(("product", null, layout));
            return Task.CompletedTask;
        }

        public Task BuildBundleAsync(PackageRequest request, WindowsPackageLayout layout, CancellationToken cancellationToken = default)
        {
            Calls.Add(("bundle", request, layout));
            return Task.CompletedTask;
        }
    }
}
