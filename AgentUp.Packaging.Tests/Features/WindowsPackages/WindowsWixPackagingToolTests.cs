using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.WindowsPackages.Models;
using AgentUp.Packaging.Features.WindowsPackages.Providers;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Tests.Features.WindowsPackages;

[TestFixture]
public class WindowsWixPackagingToolTests
{
    [Test]
    public async Task BuildStepsInvokeExpectedWixCommands()
    {
        var commands = new RecordingCommandRunner();
        var request = new PackageRequest("/repo", "windows", "win-x64", "1.2.3", "out", "Release");
        var layout = WindowsPackageLayout.From(request);
        var tool = new WindowsWixPackagingTool(commands);

        await tool.AcceptWixLicenseAsync();
        await tool.BuildProductMsiAsync(layout);
        await tool.BuildBundleAsync(request, layout);

        Assert.That(commands.Commands.Count(command => command.FileName == "wix"), Is.EqualTo(3));
        Assert.That(commands.Commands[0].Arguments, Is.EqualTo(new[] { "eula", "accept", "wix7" }));
        Assert.That(commands.Commands[1].Arguments, Does.Contain(layout.ProductWxsPath));
        Assert.That(commands.Commands[1].Arguments, Does.Contain(layout.ProductMsiPath));
        Assert.That(commands.Commands[2].Arguments, Does.Contain(layout.BundleWxsPath));
        Assert.That(commands.Commands[2].Arguments, Does.Contain("WixToolset.Bal.wixext"));
        Assert.That(commands.Commands[2].Arguments, Does.Contain(layout.SetupExePath));
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
