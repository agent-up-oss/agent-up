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

        Assert.That(CommandBytes(commands.Commands), Is.EqualTo(CommandBytes(ExpectedAgentUpWixCommands(layout))));
    }

    private static IReadOnlyList<CommandSpec> ExpectedAgentUpWixCommands(WindowsPackageLayout layout)
    {
        string[] accept = ["eula", "accept", "wix7"];
        string[] product =
        [
            "build",
            layout.ProductWxsPath,
            "-arch", "x64",
            "-o", layout.ProductMsiPath
        ];
        string[] bundle =
        [
            "build",
            layout.BundleWxsPath,
            "-ext", "WixToolset.Bal.wixext",
            "-o", layout.SetupExePath
        ];

        if (OperatingSystem.IsWindows())
        {
            return
            [
                new CommandSpec("cmd.exe", ["/c", "wix", .. accept]),
                new CommandSpec("cmd.exe", ["/c", "wix", .. product]),
                new CommandSpec("cmd.exe", ["/c", "wix", .. bundle])
            ];
        }

        return
        [
            new CommandSpec("wix", accept),
            new CommandSpec("wix", product),
            new CommandSpec("wix", bundle)
        ];
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

    private static IReadOnlyList<string> CommandBytes(IEnumerable<CommandSpec> commands)
        => commands.Select(command => string.Join('\u001f',
            [command.FileName, command.WorkingDirectory ?? "", .. command.Arguments])).ToArray();
}
