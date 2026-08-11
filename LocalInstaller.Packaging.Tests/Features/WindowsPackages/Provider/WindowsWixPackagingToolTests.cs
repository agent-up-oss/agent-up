using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.WindowsPackages.Models;
using LocalInstaller.Packaging.Features.WindowsPackages.Providers;
using LocalInstaller.Packaging.Shared.Interfaces;
using LocalInstaller.Packaging.Tests.Support;

namespace LocalInstaller.Packaging.Tests.Features.WindowsPackages.Provider;

[TestFixture]
public class WindowsWixPackagingToolTests
{
    [Test]
    public async Task BuildStepsInvokeExpectedWixCommands()
    {
        var root = Path.GetFullPath(Path.Join(Path.GetTempPath(), "pkg-wix", Guid.NewGuid().ToString("N")));

        try
        {
            Directory.CreateDirectory(root);
            var commands = new RecordingCommandRunner();
            var request = new PackageRequest(root, "windows", "win-x64", "1.2.3", "out", "Release", AgentUpPackageTestManifests.Product());
            var layout = WindowsPackageLayout.From(request);
            var tool = new WindowsWixPackagingTool(commands);

            // On Windows, BuildBundleAsync resolves WixToolset.Bal.wixext to a staged DLL.
            // Pre-create the file so the staging step skips the NuGet download.
            var extensionDll = OperatingSystem.IsWindows()
                ? Path.GetFullPath(Path.Join(root, "packaging", "windows", ".wix", "extensions",
                    "WixToolset.Bal.wixext", "7.0.0", "wixext7", "WixToolset.BootstrapperApplications.wixext.dll"))
                : null;
            if (extensionDll is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(extensionDll)!);
                File.WriteAllBytes(extensionDll, []);
            }

            await tool.AcceptWixLicenseAsync();
            await tool.BuildProductMsiAsync(layout);
            await tool.BuildBundleAsync(request, layout);

            Assert.That(CommandBytes(commands.Commands), Is.EqualTo(CommandBytes(ExpectedAgentUpWixCommands(layout, extensionDll))));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static IReadOnlyList<CommandSpec> ExpectedAgentUpWixCommands(WindowsPackageLayout layout, string? extensionDll = null)
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
            "-ext", extensionDll ?? "WixToolset.Bal.wixext",
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
