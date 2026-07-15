using AgentUp.Packaging.Features.MacOs;
using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Tests.Features.MacOs;

[TestFixture]
public class MacOsPackagerTests
{
    [Test]
    public async Task PackageAsync_invokesPkgbuildForComponentsAndProductbuildForFinalPkg()
    {
        var commands = new RecordingCommandRunner();
        var writer = new RecordingMacOsPackageWriter();
        var root = Path.Combine(Path.GetTempPath(), "AgentUp-MacOsPackagerTests", Guid.NewGuid().ToString());
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
        Assert.That(commands.Commands.Last().Arguments, Does.Contain(Path.Combine(root, "out", "agent-up-macos-osx-arm64.pkg")));
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
