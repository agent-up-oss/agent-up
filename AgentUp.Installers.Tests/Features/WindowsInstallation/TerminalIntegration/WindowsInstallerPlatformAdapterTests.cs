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
using System.Text;
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
        var scripts = PowerShellScripts(commands).ToArray();
        Assert.That(scripts.Any(script => script.Contains("Get-Service -Name $serviceName", StringComparison.Ordinal)), Is.True);
        Assert.That(scripts.Any(script => script.Contains("SetEnvironmentVariable('Path'", StringComparison.Ordinal)), Is.True);
        Assert.That(scripts.Any(script => script.Contains("CreateShortcut", StringComparison.Ordinal)), Is.True);
        Assert.That(files.Writes[@"C:\Program Files\Agent-Up\uninstall-agent-up.ps1"], Does.Contain("Remove-Item -Recurse -Force"));
        Assert.That(scripts.Any(script =>
            script.Contains(@"CurrentVersion\Uninstall\Agent-Up", StringComparison.Ordinal) &&
            script.Contains("DisplayIcon", StringComparison.Ordinal) &&
            script.Contains("NoModify", StringComparison.Ordinal) &&
            script.Contains("NoRepair", StringComparison.Ordinal)), Is.True);
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
        Assert.That(PowerShellScripts(commands).Any(script => script.Contains("Get-Command agent-up", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public async Task ExecuteComponentActionAsync_uninstallServerStopsServiceAndDeletesDirectory()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, files);

        await adapter.ExecuteComponentActionAsync(ProductComponent.Server, InstallerComponentAction.Uninstall, Session()).DrainAsync();

        Assert.That(PowerShellScripts(commands).Any(script => script.Contains("sc.exe delete $serviceName", StringComparison.Ordinal)), Is.True);
        Assert.That(files.DeletedDirectories, Does.Contain(@"C:\Program Files\Agent-Up\server"));
    }

    [Test]
    public async Task ExecuteComponentActionAsync_uninstallCliRemovesPathAndDeletesDirectories()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, files);

        await adapter.ExecuteComponentActionAsync(ProductComponent.Cli, InstallerComponentAction.Uninstall, Session()).DrainAsync();

        Assert.That(PowerShellScripts(commands).Any(script => script.Contains("GetEnvironmentVariable('Path'", StringComparison.Ordinal) && script.Contains("-ine $target", StringComparison.Ordinal)), Is.True);
        Assert.That(files.DeletedDirectories, Does.Contain(@"C:\Program Files\Agent-Up\cli"));
        Assert.That(files.DeletedDirectories, Does.Contain(@"C:\Program Files\Agent-Up\bin"));
    }

    [Test]
    public async Task ExecuteComponentActionAsync_uninstallDesktopRemovesShortcutAndDeletesDirectory()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, files);

        await adapter.ExecuteComponentActionAsync(ProductComponent.Desktop, InstallerComponentAction.Uninstall, Session()).DrainAsync();

        Assert.That(files.DeletedFiles, Does.Contain(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Agent-Up\Agent-Up.lnk"));
        Assert.That(files.DeletedDirectories, Does.Contain(@"C:\Program Files\Agent-Up\desktop"));
        Assert.That(PowerShellScripts(commands).Any(script => script.Contains("sc.exe delete", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public async Task ExecuteComponentActionAsync_doesNotPrepareWindowsServiceForDesktopInstall()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, files);

        await adapter.ExecuteComponentActionAsync(ProductComponent.Desktop, InstallerComponentAction.Install, Session()).DrainAsync();

        Assert.That(PowerShellScripts(commands).Any(script => script.Contains("Get-Service -Name $serviceName", StringComparison.Ordinal)), Is.False);
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/desktop", @"C:\Program Files\Agent-Up\desktop")));
    }

    [Test]
    public async Task ExecuteComponentActionAsync_preparesWindowsServiceForServerInstall()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, files);

        await adapter.ExecuteComponentActionAsync(ProductComponent.Server, InstallerComponentAction.Install, Session()).DrainAsync();

        Assert.That(PowerShellScripts(commands).Any(script =>
            script.Contains("Get-Service -Name $serviceName", StringComparison.Ordinal) &&
            script.Contains("exit 0", StringComparison.Ordinal)), Is.True);
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/server", @"C:\Program Files\Agent-Up\server")));
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
            Assert.That(product, Does.Contain("Id=\"ARPNOMODIFY\""));
            Assert.That(product, Does.Contain("Id=\"ARPNOREPAIR\""));
            Assert.That(product, Does.Contain("Name=\"agent-up-server\""));
            Assert.That(product, Does.Not.Contain("Start=\"install\""));
            Assert.That(product, Does.Not.Contain("Stop=\"both\""));
            Assert.That(product, Does.Contain("Stop=\"uninstall\""));
            Assert.That(product, Does.Contain("Name=\"PATH\""));
            Assert.That(product, Does.Contain("Shortcut"));
            Assert.That(product, Does.Contain("Agent-Up Installer"));
            Assert.That(product, Does.Contain("AgentUp.InstallerApp.exe"));
            Assert.That(product, Does.Contain("InstallerPayloadDesktop"));
            Assert.That(product, Does.Contain("InstallerPayloadServer"));
            Assert.That(product, Does.Contain("InstallerPayloadCli"));
            Assert.That(ComponentGuids(product), Is.Unique);
            Assert.That(bundle, Does.Contain("WixStandardBootstrapperApplication"));
            Assert.That(bundle, Does.Contain("Theme=\"rtfLicense\""));
            Assert.That(bundle, Does.Contain(@"LaunchTarget=""[ProgramFiles64Folder]Agent-Up\installer\AgentUp.InstallerApp.exe"""));
            Assert.That(bundle, Does.Contain(@"LaunchWorkingFolder=""[ProgramFiles64Folder]Agent-Up\installer"""));
            Assert.That(bundle, Does.Contain("MsiPackage"));
            Assert.That(bundle, Does.Contain("Product.msi"));
            Assert.That(bundle, Does.Not.Contain("ExePackage"));
            Assert.That(bundle, Does.Not.Contain("InstallArguments"));
            Assert.That(bundle, Does.Not.Contain("UninstallArguments"));
            Assert.That(bundle, Does.Not.Contain("RegistrySearch"));
            Assert.That(bundle, Does.Not.Contain("Win64="));
            Assert.That(bundle, Does.Not.Contain("Permanent=\"yes\""));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void WindowsWixSourceGenerator_forNonAgentUpManifest_containsProductIdentityAndNoAgentUpBranding()
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
            WritePublishedFile(layout.ServerPublishDirectory, "AgentUp.Server.exe");
            WritePublishedFile(layout.CliPublishDirectory, "AgentUp.CLI.exe");
            Directory.CreateDirectory(layout.InstallerSourceDirectory);
            File.WriteAllText(System.IO.Path.Join(layout.InstallerSourceDirectory, "acme-studio.cmd"), "");
            var manifest = WindowsInstallerManifest.From(AcmeStudio(), "1.2.3", "http://127.0.0.1:5001");

            var generator = new WindowsWixSourceGenerator(manifest);
            var product = generator.ProductWxs(layout);
            var bundle = generator.BundleWxs(layout);
            var output = product + Environment.NewLine + bundle;

            Assert.Multiple(() =>
            {
                Assert.That(product, Does.Contain("Name=\"Acme Studio\""));
                Assert.That(product, Does.Contain("Manufacturer=\"Acme Studio\""));
                Assert.That(product, Does.Contain("Name=\"acme-studio-server\""));
                Assert.That(product, Does.Contain("DisplayName=\"Acme Studio Server\""));
                Assert.That(product, Does.Contain("Local Acme Studio runtime authority"));
                Assert.That(product, Does.Contain("Acme Studio Installer"));
                Assert.That(product, Does.Contain("Software\\Acme Studio"));
                Assert.That(product, Does.Contain("acme-studio.cmd"));
                Assert.That(bundle, Does.Contain(@"LaunchTarget=""[ProgramFiles64Folder]Acme Studio\installer\AgentUp.InstallerApp.exe"""));
                Assert.That(bundle, Does.Contain(@"LaunchWorkingFolder=""[ProgramFiles64Folder]Acme Studio\installer"""));
                Assert.That(output, Does.Not.Contain("Agent-Up"));
                Assert.That(output, Does.Not.Contain("agent-up"));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestCase(@"..\outside.cmd")]
    [TestCase("orbit?.cmd")]
    [TestCase("CON.cmd")]
    [TestCase("shim.cmd.")]
    [TestCase("shim.cmd ")]
    public void WindowsWixSourceGenerator_rejectsUnsafeCliShimNameBeforeBuildingSourcePath(string cliShimName)
    {
        var manifest = WindowsInstallerManifest.Create("1.2.3") with { CliShimName = cliShimName };

        var exception = Assert.Throws<ArgumentException>(() => new WindowsWixSourceGenerator(manifest));

        Assert.That(exception!.ParamName, Is.EqualTo("cliShimName"));
    }

    [TestCase(@"..\outside.cmd")]
    [TestCase("orbit?.cmd")]
    [TestCase("CON.cmd")]
    [TestCase("shim.cmd.")]
    [TestCase("shim.cmd ")]
    public void WindowsInstallerPaths_rejectsUnsafeCliShimNameBeforeBuildingPath(string cliShimName)
    {
        var paths = Options().Paths;

        var exception = Assert.Throws<ArgumentException>(() => paths.CliShimPathFor(cliShimName));

        Assert.That(exception!.ParamName, Is.EqualTo("cliShimName"));
    }

    [Test]
    public async Task ExecuteInstallAsync_withNonAgentUpManifest_registersProductServiceAndDoesNotQueryAgentUpService()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = AcmeStudioAdapter(commands, files);
        var session = AcmeStudioSession();

        await foreach (var progress in adapter.ExecuteInstallAsync(session)) { _ = progress; }
        await adapter.ValidateInstalledStateAsync(session);

        var scArguments = commands.Commands.Where(c => c.FileName == "sc.exe").Select(c => c.Arguments).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(scArguments.Any(a => a.Contains("acme-studio-server")), Is.True, "Should register Acme Studio service");
            Assert.That(scArguments.Any(a => a.Contains("agent-up")), Is.False, "Should not reference Agent-Up service name");
            Assert.That(files.Writes.Keys, Does.Contain(@"C:\Program Files\Acme Studio\bin\acme-studio.cmd"));
            Assert.That(files.Writes.Keys, Does.Not.Contain(@"C:\Program Files\Acme Studio\bin\agent-up.cmd"));
        });
    }

    [Test]
    public async Task ExecuteInstallAsync_uninstallEntryCarriesProductDisplayName_andTwoProductEntriesCoexistWithoutOverwrite()
    {
        var agentUpCommands = new RecordingCommandRunner();
        var acmeCommands = new RecordingCommandRunner();

        await foreach (var progress in Adapter(agentUpCommands, new RecordingWindowsFileSystem()).ExecuteInstallAsync(Session())) { _ = progress; }
        await foreach (var progress in AcmeStudioAdapter(acmeCommands, new RecordingWindowsFileSystem()).ExecuteInstallAsync(AcmeStudioSession())) { _ = progress; }

        var agentUpRegistryScript = PowerShellScripts(agentUpCommands).First(s => s.Contains("Uninstall"));
        var acmeRegistryScript = PowerShellScripts(acmeCommands).First(s => s.Contains("Uninstall"));
        Assert.Multiple(() =>
        {
            Assert.That(agentUpRegistryScript, Does.Contain(@"Uninstall\Agent-Up"), "Agent-Up entry should use its own key");
            Assert.That(agentUpRegistryScript, Does.Not.Contain("Acme Studio"), "Agent-Up entry should not contain Acme Studio");
            Assert.That(acmeRegistryScript, Does.Contain(@"Uninstall\Acme Studio"), "Acme Studio entry should use its own key");
            Assert.That(acmeRegistryScript, Does.Not.Contain("Agent-Up"), "Acme Studio entry should not contain Agent-Up");
        });
    }

    [Test]
    public async Task ExecuteInstallAsync_withNonAgentUpManifest_pathEntryPointsToProductDirectoryNotAgentUp()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = AcmeStudioAdapter(commands, files);

        await foreach (var progress in adapter.ExecuteInstallAsync(AcmeStudioSession())) { _ = progress; }

        var pathScript = PowerShellScripts(commands).First(s => s.Contains("SetEnvironmentVariable"));
        Assert.Multiple(() =>
        {
            Assert.That(pathScript, Does.Contain(@"C:\Program Files\Acme Studio\bin"), "PATH should reference Acme Studio directory");
            Assert.That(pathScript, Does.Not.Contain("Agent-Up"), "PATH script should not reference Agent-Up");
        });
    }

    [Test]
    public async Task ExecuteInstallAsync_withNonAgentUpManifest_uninstallScriptRemovesProductEntriesNotAgentUpEntries()
    {
        var files = new RecordingWindowsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = AcmeStudioAdapter(commands, files);

        await foreach (var progress in adapter.ExecuteInstallAsync(AcmeStudioSession())) { _ = progress; }

        var uninstallScript = files.Writes.Values.First(v => v.Contains("sc.exe stop"));
        Assert.Multiple(() =>
        {
            Assert.That(uninstallScript, Does.Contain("acme-studio-server"), "Uninstall should stop/delete Acme Studio service");
            Assert.That(uninstallScript, Does.Contain(@"Uninstall\Acme Studio"), "Uninstall should remove Acme Studio registry key");
            Assert.That(uninstallScript, Does.Not.Contain("agent-up-server"), "Uninstall must not reference Agent-Up service");
            Assert.That(uninstallScript, Does.Not.Contain("Agent-Up"), "Uninstall must not reference Agent-Up");
        });
    }

    [TestCaseSource(nameof(TwoManifestCases))]
    public async Task ExecuteInstallAsync_forTwoDistinctManifests_leavesDistinctAndIntactEntries(
        ProductManifest first, WindowsInstallerOptions firstOptions,
        ProductManifest second, WindowsInstallerOptions secondOptions)
    {
        var firstCommands = new RecordingCommandRunner();
        var secondCommands = new RecordingCommandRunner();
        var firstSession = InstallerSession.CreateDefault(first, new Version(1, 2, 3), firstOptions.Paths.RootDirectory, PayloadSelection.Bundled(first.ProductName, new Version(1, 2, 3)));
        var secondSession = InstallerSession.CreateDefault(second, new Version(1, 2, 3), secondOptions.Paths.RootDirectory, PayloadSelection.Bundled(second.ProductName, new Version(1, 2, 3)));
        var firstAdapter = new WindowsInstallerPlatformAdapter(firstCommands, new RecordingWindowsFileSystem(), firstOptions, new RequiredCommandRunner(firstCommands), new DockerPrerequisite(new DockerPrerequisiteProvider(firstCommands), new Version(27, 0, 0)));
        var secondAdapter = new WindowsInstallerPlatformAdapter(secondCommands, new RecordingWindowsFileSystem(), secondOptions, new RequiredCommandRunner(secondCommands), new DockerPrerequisite(new DockerPrerequisiteProvider(secondCommands), new Version(27, 0, 0)));

        await foreach (var progress in firstAdapter.ExecuteInstallAsync(firstSession)) { _ = progress; }
        await foreach (var progress in secondAdapter.ExecuteInstallAsync(secondSession)) { _ = progress; }

        var firstSc = firstCommands.Commands.Where(c => c.FileName == "sc.exe").Select(c => c.Arguments).ToList();
        var secondSc = secondCommands.Commands.Where(c => c.FileName == "sc.exe").Select(c => c.Arguments).ToList();
        var firstPathScript = PowerShellScripts(firstCommands).First(s => s.Contains("SetEnvironmentVariable"));
        var secondPathScript = PowerShellScripts(secondCommands).First(s => s.Contains("SetEnvironmentVariable"));
        var firstRegistryScript = PowerShellScripts(firstCommands).First(s => s.Contains("Uninstall"));
        var secondRegistryScript = PowerShellScripts(secondCommands).First(s => s.Contains("Uninstall"));

        Assert.Multiple(() =>
        {
            Assert.That(first.ServiceName, Is.Not.EqualTo(second.ServiceName), "Service names must differ");
            Assert.That(firstOptions.Paths.BinDirectory, Is.Not.EqualTo(secondOptions.Paths.BinDirectory), "Bin directories must differ");

            Assert.That(firstSc.Any(a => a.Contains(first.ServiceName)), Is.True, $"First install must use {first.ServiceName}");
            Assert.That(firstSc.Any(a => a.Contains(second.ServiceName)), Is.False, $"First install must not touch {second.ServiceName}");
            Assert.That(secondSc.Any(a => a.Contains(second.ServiceName)), Is.True, $"Second install must use {second.ServiceName}");
            Assert.That(secondSc.Any(a => a.Contains(first.ServiceName)), Is.False, $"Second install must not touch {first.ServiceName}");

            Assert.That(firstPathScript, Does.Contain(firstOptions.Paths.BinDirectory));
            Assert.That(firstPathScript, Does.Not.Contain(secondOptions.Paths.BinDirectory));
            Assert.That(secondPathScript, Does.Contain(secondOptions.Paths.BinDirectory));
            Assert.That(secondPathScript, Does.Not.Contain(firstOptions.Paths.BinDirectory));

            Assert.That(firstRegistryScript, Does.Contain($@"Uninstall\{first.ProductName}"));
            Assert.That(firstRegistryScript, Does.Not.Contain($@"Uninstall\{second.ProductName}"));
            Assert.That(secondRegistryScript, Does.Contain($@"Uninstall\{second.ProductName}"));
            Assert.That(secondRegistryScript, Does.Not.Contain($@"Uninstall\{first.ProductName}"));
        });
    }

    private static IEnumerable<TestCaseData> TwoManifestCases()
    {
        yield return new TestCaseData(
            ProductManifest.AgentUp(), Options(),
            AcmeStudio(), AcmeStudioOptions())
            .SetName("AgentUp_vs_AcmeStudio");
    }

    private static ProductManifest AcmeStudio()
        => new("Acme Studio", "acme-studio", "ACMESTUDIO");

    private static InstallerSession AcmeStudioSession()
        => InstallerSession.CreateDefault(AcmeStudio(), new Version(1, 2, 3), @"C:\Program Files\Acme Studio", PayloadSelection.Bundled("Acme Studio", new Version(1, 2, 3)))
            with { ServerUrl = "http://127.0.0.1:5001" };

    private static WindowsInstallerOptions AcmeStudioOptions()
        => new(
            new WindowsInstallPayload("/payload/desktop", "/payload/server", "/payload/cli"),
            new WindowsInstallerPaths(
                RootDirectory: @"C:\Program Files\Acme Studio",
                DesktopDirectory: @"C:\Program Files\Acme Studio\desktop",
                ServerDirectory: @"C:\Program Files\Acme Studio\server",
                CliDirectory: @"C:\Program Files\Acme Studio\cli",
                BinDirectory: @"C:\Program Files\Acme Studio\bin",
                StartMenuShortcutPath: @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Acme Studio\Acme Studio.lnk")
            { UninstallScriptName = "uninstall-acme-studio.ps1" });

    private static WindowsInstallerPlatformAdapter AcmeStudioAdapter(
        RecordingCommandRunner commands,
        RecordingWindowsFileSystem files)
        => new(
            commands,
            files,
            AcmeStudioOptions(),
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));

    private static InstallerSession Session()
        => InstallerSession.CreateDefault(ProductManifest.AgentUp(), new Version(1, 2, 3), @"C:\Program Files\Agent-Up", PayloadSelection.Bundled(new Version(1, 2, 3)));

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

    private static IEnumerable<string> PowerShellScripts(RecordingCommandRunner commands)
        => commands.Commands
            .Where(command => command.FileName == "powershell.exe")
            .Select(PowerShellScript);

    private static string PowerShellScript((string FileName, string Arguments) command)
    {
        var parts = command.Arguments.Split("-EncodedCommand ", StringSplitOptions.None);
        return parts.Length == 2
            ? Encoding.Unicode.GetString(Convert.FromBase64String(parts[1]))
            : command.Arguments;
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
        public List<string> DeletedDirectories { get; } = [];
        public List<string> DeletedFiles { get; } = [];
        public List<string> CreatedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedDirectories { get; } = [];
        public Dictionary<string, string> Writes { get; } = [];
        public HashSet<string> ExistingFiles { get; } = [];

        public void ResetDirectory(string path) => ResetDirectories.Add(path);
        public void DeleteDirectory(string path) => DeletedDirectories.Add(path);
        public void DeleteFile(string path) => DeletedFiles.Add(path);
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
