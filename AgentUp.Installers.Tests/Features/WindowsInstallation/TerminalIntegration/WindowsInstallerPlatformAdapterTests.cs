using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
using AgentUp.Installers.Features.WindowsInstallation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using AgentUp.Installers.Features.WindowsInstallation.Providers;
using System.Xml.Linq;

namespace AgentUp.Installers.Tests.Features.WindowsInstallation.TerminalIntegration;

[TestFixture]
public class WindowsInstallerPlatformAdapterTests
{
    [Test]
    public async Task ExecuteInstallAsync_appliesPayloadAndRegistersServicePathAndShortcut()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, files);
        var session = Session();

        var progress = new List<InstallProgress>();
        await foreach (var item in adapter.ExecuteInstallAsync(session))
            progress.Add(item);

        Assert.That(progress.Select(item => item.Kind), Is.EqualTo(new[]
        {
            InstallOperationKind.ValidatePrerequisites,
            InstallOperationKind.StagePayload,
            InstallOperationKind.InstallFiles,
            InstallOperationKind.RegisterService,
            InstallOperationKind.RegisterCli,
            InstallOperationKind.RegisterDesktop,
            InstallOperationKind.RegisterUninstall,
            InstallOperationKind.ValidateInstallation
        }));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/desktop", @"C:\Program Files\Agent-Up\desktop")));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/server", @"C:\Program Files\Agent-Up\server")));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/cli", @"C:\Program Files\Agent-Up\cli")));
        Assert.That(files.Writes[@"C:\Program Files\Agent-Up\bin\agent-up.cmd"], Does.Contain("AgentUp.CLI.exe"));
        Assert.That(commands.Commands, Does.Contain(("sc.exe", "start agent-up-server")));
        Assert.That(commands.Commands.Any(command => command.FileName == "sc.exe" && command.Arguments.Contains("create agent-up-server", StringComparison.Ordinal)), Is.True);
        Assert.That(commands.Commands.Any(command => command.FileName == "powershell.exe" && command.Arguments.Contains("SetEnvironmentVariable('Path'", StringComparison.Ordinal)), Is.True);
        Assert.That(commands.Commands.Any(command => command.FileName == "powershell.exe" && command.Arguments.Contains("CreateShortcut", StringComparison.Ordinal)), Is.True);
        Assert.That(files.Writes[@"C:\Program Files\Agent-Up\uninstall-agent-up.ps1"], Does.Contain("Remove-Item -Recurse -Force"));
        Assert.That(commands.Commands.Any(command => command.FileName == "powershell.exe" && command.Arguments.Contains(@"CurrentVersion\Uninstall\Agent-Up", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public async Task ValidateInstalledStateAsync_reportsSuccessFromWindowsState()
    {
        var files = new RecordingWindowsFileSystem();
        files.ExistingFiles.Add(@"C:\Program Files\Agent-Up\desktop\AgentUp.Desktop.exe");
        var commands = new RecordingCommandRunner();
        commands.Results.Enqueue(new ProcessResult(0, "STATE              : 4  RUNNING", ""));
        commands.Results.Enqueue(new ProcessResult(0, "", ""));
        var adapter = Adapter(commands, files);

        var report = await adapter.ValidateInstalledStateAsync(Session());

        Assert.That(report.Succeeded, Is.True);
        Assert.That(commands.Commands, Does.Contain(("sc.exe", "query agent-up-server")));
        Assert.That(commands.Commands.Any(command => command.FileName == "powershell.exe" && command.Arguments.Contains("Get-Command agent-up", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void WindowsWixSourceGenerator_containsServicePathShortcutAndBundleContract()
    {
        var root = System.IO.Path.Join(System.IO.Path.GetTempPath(), "AgentUp-WindowsInstallerTests", Guid.NewGuid().ToString());
        try
        {
            var layout = new WindowsInstallerLayout(
                InstallerSourceDirectory: System.IO.Path.Join(root, "wix"),
                InstallerPublishDirectory: System.IO.Path.Join(root, "installer"),
                DesktopPublishDirectory: System.IO.Path.Join(root, "desktop"),
                ServerPublishDirectory: System.IO.Path.Join(root, "server"),
                CliPublishDirectory: System.IO.Path.Join(root, "cli"),
                LicenseRtfPath: System.IO.Path.Join(root, "wix", "License.rtf"),
                ProductMsiPath: System.IO.Path.Join(root, "Product.msi"));
            WritePublishedFile(layout.InstallerPublishDirectory, "AgentUp.InstallerApp.exe");
            WritePublishedFile(layout.DesktopPublishDirectory, "AgentUp.Desktop.exe");
            WritePublishedFile(layout.DesktopPublishDirectory, "AgentUp.Shared.dll");
            WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Server.exe");
            WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Shared.dll");
            WritePublishedFile(layout.CliPublishDirectory, "AgentUp.CLI.exe");
            WritePublishedFile(layout.CliPublishDirectory, "AgentUp.Shared.dll");
            Directory.CreateDirectory(layout.InstallerSourceDirectory);
            File.WriteAllText(System.IO.Path.Join(layout.InstallerSourceDirectory, "agent-up.cmd"), "");

            var generator = new WindowsWixSourceGenerator(WindowsInstallerManifest.Create("1.2.3"));
            var product = generator.ProductWxs(layout);
            var bundle = generator.BundleWxs(layout);

            Assert.That(product, Does.Contain("ServiceInstall"));
            Assert.That(product, Does.Contain("EmbedCab=\"yes\""));
            Assert.That(product, Does.Contain("Name=\"agent-up-server\""));
            Assert.That(product, Does.Not.Contain("Start=\"install\""));
            Assert.That(product, Does.Not.Contain("Stop=\"both\""));
            Assert.That(product, Does.Contain("Stop=\"uninstall\""));
            Assert.That(product, Does.Contain("Name=\"PATH\""));
            Assert.That(product, Does.Contain("Shortcut"));
            Assert.That(ComponentGuids(product), Is.Unique);
            Assert.That(bundle, Does.Contain("WixStandardBootstrapperApplication"));
            Assert.That(bundle, Does.Contain("Theme=\"rtfLicense\""));
            Assert.That(bundle, Does.Contain("ExePackage"));
            Assert.That(bundle, Does.Contain("AgentUp.InstallerApp.exe"));
            Assert.That(bundle, Does.Not.Contain("MsiPackage"));
            Assert.That(bundle, Does.Contain("payload\\desktop\\AgentUp.Desktop.exe"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static InstallerSession Session()
        => InstallerSession.CreateDefault("Agent-Up", new Version(1, 2, 3), @"C:\Program Files\Agent-Up", PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static WindowsInstallerOptions Options()
        => new(
            new WindowsInstallPayload("/payload/desktop", "/payload/server", "/payload/cli"),
            new WindowsInstallerPaths(
                RootDirectory: @"C:\Program Files\Agent-Up",
                DesktopDirectory: @"C:\Program Files\Agent-Up\desktop",
                ServerDirectory: @"C:\Program Files\Agent-Up\server",
                CliDirectory: @"C:\Program Files\Agent-Up\cli",
                BinDirectory: @"C:\Program Files\Agent-Up\bin",
                StartMenuShortcutPath: @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Agent-Up\Agent-Up.lnk"));

    private static WindowsInstallerPlatformAdapter Adapter(
        RecordingCommandRunner commands,
        RecordingWindowsFileSystem files)
        => new(
            commands,
            files,
            Options(),
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));

    private static void WritePublishedFile(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(System.IO.Path.Join(directory, name), "test");
    }

    private static IEnumerable<string> ComponentGuids(string productWxs)
    {
        XNamespace wix = "http://wixtoolset.org/schemas/v4/wxs";
        return XDocument.Parse(productWxs)
            .Descendants(wix + "Component")
            .Select(component => (string?)component.Attribute("Guid"))
            .Where(guid => !string.IsNullOrWhiteSpace(guid))!;
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<(string FileName, string Arguments)> Commands { get; } = [];
        public Queue<ProcessResult> Results { get; } = [];

        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            Commands.Add((fileName, arguments));
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new ProcessResult(0, "", ""));
        }
    }

    private sealed class RecordingWindowsFileSystem : IWindowsInstallerFileSystem
    {
        public List<string> ResetDirectories { get; } = [];
        public List<string> CreatedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedDirectories { get; } = [];
        public Dictionary<string, string> Writes { get; } = [];
        public HashSet<string> ExistingFiles { get; } = [];

        public void ResetDirectory(string path) => ResetDirectories.Add(path);
        public void CreateDirectory(string path) => CreatedDirectories.Add(path);
        public void CopyDirectory(string source, string destination) => CopiedDirectories.Add((source, destination));
        public void WriteText(string path, string text)
        {
            Writes[path] = text;
            ExistingFiles.Add(path);
        }
        public bool FileExists(string path) => ExistingFiles.Contains(path);
    }
}
