using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.MacOsPackages.Providers;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Tests.Features.MacOsPackages;

[TestFixture]
public class MacOsPackageToolTests
{
    [Test]
    public async Task BuildAsyncInvokesPkgbuildAndProductbuild()
    {
        var commands = new RecordingCommandRunner();
        var request = new PackageRequest("/repo", "macos", "osx-arm64", "1.2.3", "out", "Release");
        var layout = MacOsPackageLayout.From(request);
        var tool = new MacOsPackageTool(commands);

        await tool.BuildComponentPackagesAsync(request, layout);
        await tool.BuildProductPackageAsync(layout);

        Assert.That(commands.Commands.Count(command => command.FileName == "pkgbuild"), Is.EqualTo(4));
        Assert.That(commands.Commands.Last().FileName, Is.EqualTo("productbuild"));
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.installer")), Is.True);
        Assert.That(commands.Commands.First().Arguments, Does.Contain("--scripts"));
        Assert.That(commands.Commands.First().Arguments, Does.Contain(layout.InstallerScriptsDirectory));
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.desktop")), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.cli")), Is.True);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.server")), Is.True);
        Assert.That(commands.Commands.Last().Arguments, Does.Contain(Path.Join("/repo", "out", "agent-up-macos-osx-arm64.pkg")));
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
