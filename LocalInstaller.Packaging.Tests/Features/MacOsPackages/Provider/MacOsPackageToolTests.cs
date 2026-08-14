using LocalInstaller.Packaging.Features.MacOsPackages.Models;
using LocalInstaller.Packaging.Features.MacOsPackages.Providers;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Shared.Interfaces;
using LocalInstaller.Packaging.Tests.Support;

namespace LocalInstaller.Packaging.Tests.Features.MacOsPackages.Provider;

[TestFixture]
public class MacOsPackageToolTests
{
    private static readonly string Root = Path.GetFullPath(Path.Join(Path.GetTempPath(), "pkg"));
    [Test]
    public async Task BuildAsyncInvokesPkgbuildAndProductbuild()
    {
        var commands = new RecordingCommandRunner();
        var request = new PackageRequest(Root, "macos", "osx-arm64", "1.2.3", "out", "Release", AgentUpPackageTestManifests.Product());
        var layout = MacOsPackageLayout.From(request);
        var manifest = MacOsPackageManifest.From(request);
        var tool = new MacOsPackageTool(commands);

        await tool.BuildComponentPackagesAsync(request, layout, manifest);
        await tool.BuildProductPackageAsync(layout);

        Assert.That(commands.Commands.Count(command => command.FileName == "pkgbuild"), Is.EqualTo(1));
        Assert.That(commands.Commands.Last().FileName, Is.EqualTo("productbuild"));
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.installer")), Is.True);
        Assert.That(commands.Commands.First().Arguments, Does.Contain("--scripts"));
        Assert.That(commands.Commands.First().Arguments, Does.Contain(layout.InstallerScriptsDirectory));
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.desktop")), Is.False);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.cli")), Is.False);
        Assert.That(commands.Commands.Any(command => command.Arguments.Contains("dev.agent-up.server")), Is.False);
        Assert.That(commands.Commands.Last().Arguments, Does.Contain(Path.Join(Root, "out", "agent-up-macos-osx-arm64.pkg")));
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
