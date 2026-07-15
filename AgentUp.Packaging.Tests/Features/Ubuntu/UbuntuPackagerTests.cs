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
