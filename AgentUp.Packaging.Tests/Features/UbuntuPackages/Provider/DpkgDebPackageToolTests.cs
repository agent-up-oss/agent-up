using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Features.UbuntuPackages.Providers;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Tests.Features.UbuntuPackages.Provider;

[TestFixture]
public class DpkgDebPackageToolTests
{
    private static readonly string Root = Path.GetFullPath("repo");
    [Test]
    public async Task BuildDebAsyncInvokesDpkgDebBuild()
    {
        var commands = new RecordingCommandRunner();
        var request = new PackageRequest(Root, "ubuntu", "linux-x64", "1.2.3", "out", "Release");
        var layout = UbuntuPackageLayout.From(request);

        await new DpkgDebPackageTool(commands).BuildDebAsync(layout);

        Assert.That(commands.Commands.Single().FileName, Is.EqualTo("dpkg-deb"));
        Assert.That(commands.Commands.Single().Arguments, Is.EqualTo(new[]
        {
            "--build",
            Path.Join(Root, "artifacts", "stage", "ubuntu-linux-x64", "deb-root"),
            Path.Join(Root, "out", "agent-up-ubuntu-linux-x64.deb")
        }));
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
}
