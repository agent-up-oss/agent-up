using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.Ubuntu;

namespace AgentUp.Packaging.Tests.Features.Ubuntu;

[TestFixture]
public class UbuntuPackagerTests
{
    [Test]
    public async Task PackageAsync_invokesDotNetPublishesAndDpkgBuild()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingPackageWriter();
        var request = new PackageRequest("/repo", "ubuntu", "linux-x64", "1.2.3", "out", "Release");

        await new UbuntuPackager(commands, writer).PackageAsync(request);

        Assert.That(commands.Commands.Count(command => command.FileName == "dotnet" && command.Arguments.Contains("publish")), Is.EqualTo(3));
        Assert.That(commands.Commands.Last().FileName, Is.EqualTo("dpkg-deb"));
        Assert.That(commands.Commands.Last().Arguments, Is.EqualTo(new[]
        {
            "--build",
            Path.Combine("/repo", "artifacts", "stage", "ubuntu-linux-x64", "deb-root"),
            Path.Combine("/repo", "out", "agent-up-ubuntu-linux-x64.deb")
        }));
        Assert.That(writer.CreatedDirectories, Does.Contain(Path.Combine("/repo", "out")));
    }

    [Test]
    public async Task PackageAsync_withPayloadRootCopiesPayloadAndSkipsDotNetPublish()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingPackageWriter();
        var root = Path.Combine(Path.GetTempPath(), "AgentUp-UbuntuPackagerTests", Guid.NewGuid().ToString());
        var payloadRoot = Path.Combine(root, "payload");
        var request = new PackageRequest(root, "ubuntu", "linux-x64", "1.2.3", "out", "Release", payloadRoot);

        try
        {
            WritePayloadFile(payloadRoot, "desktop", "AgentUp.Desktop");
            WritePayloadFile(payloadRoot, "server", "AgentUp.Server");
            WritePayloadFile(payloadRoot, "cli", "AgentUp.CLI");

            await new UbuntuPackager(commands, writer).PackageAsync(request);

            Assert.That(commands.Commands.Any(command => command.FileName == "dotnet"), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "artifacts", "stage", "ubuntu-linux-x64", "desktop", "AgentUp.Desktop")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "artifacts", "stage", "ubuntu-linux-x64", "server", "AgentUp.Server")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "artifacts", "stage", "ubuntu-linux-x64", "cli", "AgentUp.CLI")), Is.True);
            Assert.That(commands.Commands.Last().FileName, Is.EqualTo("dpkg-deb"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void WritePayloadFile(string payloadRoot, string component, string fileName)
    {
        var directory = Path.Combine(payloadRoot, component);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), "");
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
}
