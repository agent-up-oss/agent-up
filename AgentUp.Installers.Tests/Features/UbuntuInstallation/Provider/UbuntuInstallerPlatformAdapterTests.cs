using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.UbuntuInstallation;
using AgentUp.Installers.Features.UbuntuInstallation.DTOs;
using AgentUp.Installers.Features.UbuntuInstallation.Models;
using AgentUp.Installers.Features.UbuntuInstallation.Providers;

namespace AgentUp.Installers.Tests.Features.UbuntuInstallation.Provider;

[TestFixture]
public class UbuntuInstallerPlatformAdapterTests
{
    // ── existing tests (Agent-Up product) ────────────────────────────────────

    [Test]
    public async Task ExecuteInstallAsync_yieldsCorrectProgressSequence()
    {
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, new RecordingUbuntuFileSystem());
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
            InstallOperationKind.RegisterAutoStart,
            InstallOperationKind.RegisterUninstall,
            InstallOperationKind.ValidateInstallation
        }));
    }

    [Test]
    public async Task ExecuteInstallAsync_runsElevatedScriptContainingAllComponents()
    {
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, new RecordingUbuntuFileSystem());

        await adapter.ExecuteInstallAsync(Session()).DrainAsync();

        var script = commands.CapturedScript;
        Assert.That(script, Is.Not.Null.And.Not.Empty, "Elevated script was not executed.");

        // Desktop operations
        Assert.That(script, Does.Contain("rm -rf '/opt/agent-up/desktop'"));
        Assert.That(script, Does.Contain("cp -r '/payload/desktop'/."));
        Assert.That(script, Does.Contain("cp '/payload/logo.png' '/usr/share/pixmaps/agent-up.png'"));
        Assert.That(script, Does.Contain("chmod +x '/opt/agent-up/desktop/AgentUp.Desktop'"));
        Assert.That(script, Does.Contain("'/usr/share/applications/agent-up.desktop'"));
        Assert.That(script, Does.Contain("update-desktop-database /usr/share/applications"));

        // Server operations
        Assert.That(script, Does.Contain("rm -rf '/opt/agent-up/server'"));
        Assert.That(script, Does.Contain("cp -r '/payload/server'/."));
        Assert.That(script, Does.Contain("cp '/payload/agent-up-server.service' '/etc/systemd/system/agent-up-server.service'"));
        Assert.That(script, Does.Contain("systemctl daemon-reload"));
        Assert.That(script, Does.Contain("systemctl enable --now 'agent-up-server.service'"));

        // CLI operations
        Assert.That(script, Does.Contain("rm -rf '/opt/agent-up/cli'"));
        Assert.That(script, Does.Contain("cp -r '/payload/cli'/."));
        Assert.That(script, Does.Contain("chmod +x '/opt/agent-up/cli/AgentUp.CLI'"));
        Assert.That(script, Does.Contain("ln -sf '/opt/agent-up/cli/AgentUp.CLI' '/usr/bin/agent-up'"));
    }

    [Test]
    public async Task ValidateInstalledStateAsync_reportsSuccessFromUbuntuState()
    {
        var files = new RecordingUbuntuFileSystem();
        files.ExistingFiles.Add("/usr/share/applications/agent-up.desktop");
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, files);

        var report = await adapter.ValidateInstalledStateAsync(Session());

        Assert.That(report.Succeeded, Is.True);
        Assert.That(commands.Commands, Does.Contain(("systemctl", "is-enabled agent-up-server.service")));
        Assert.That(commands.Commands, Does.Contain(("systemctl", "is-active agent-up-server.service")));
        Assert.That(commands.Commands, Does.Contain(("bash", "-lc command -v \"$1\" -- agent-up")));
    }

    [Test]
    public void PlanInstall_marksNativeUbuntuOperationsAsElevationRequired()
    {
        var adapter = Adapter(new RecordingCommandRunner(), new RecordingUbuntuFileSystem());

        var plan = adapter.PlanInstall(Session());

        Assert.That(plan.Where(item => item.RequiresElevation).Select(item => item.Kind), Is.EquivalentTo(new[]
        {
            InstallOperationKind.InstallFiles,
            InstallOperationKind.RegisterService,
            InstallOperationKind.RegisterCli,
            InstallOperationKind.RegisterDesktop,
            InstallOperationKind.RegisterAutoStart,
            InstallOperationKind.RegisterUninstall
        }));
    }

    // ── Test 1: non-Agent-Up install registers the product's systemd unit ────

    [Test]
    public async Task ExecuteInstallAsync_withCustomManifest_registersProductServiceUnitNotAgentUpUnit()
    {
        var commands = new ScriptCapturingCommandRunner();
        var adapter = CustomAdapter(commands, new RecordingUbuntuFileSystem());

        await adapter.ExecuteInstallAsync(CustomSession()).DrainAsync();

        var script = commands.CapturedScript;
        Assert.That(script, Does.Contain("acme-studio-server.service"));
        Assert.That(script, Does.Not.Contain("agent-up-server.service"));
    }

    [Test]
    public async Task ValidateInstalledStateAsync_withCustomManifest_queriesProductServiceUnitNotAgentUpUnit()
    {
        var files = new RecordingUbuntuFileSystem();
        files.ExistingFiles.Add("/usr/share/applications/acme-studio.desktop");
        var commands = new RecordingCommandRunner();
        var adapter = CustomAdapter(commands, files);

        await adapter.ValidateInstalledStateAsync(CustomSession());

        Assert.That(commands.Commands, Does.Contain(("systemctl", "is-enabled acme-studio-server.service")));
        Assert.That(commands.Commands, Does.Contain(("systemctl", "is-active acme-studio-server.service")));
        Assert.That(commands.Commands, Does.Not.Contain(("systemctl", "is-enabled agent-up-server.service")));
    }

    // ── Test 2: CLI symlink uses the product's shim name ─────────────────────

    [Test]
    public async Task ExecuteInstallAsync_withCustomManifest_createsCliSymlinkAtProductName()
    {
        var commands = new ScriptCapturingCommandRunner();
        var adapter = CustomAdapter(commands, new RecordingUbuntuFileSystem());

        await adapter.ExecuteInstallAsync(CustomSession()).DrainAsync();

        var script = commands.CapturedScript;
        Assert.That(script, Does.Contain("'/usr/bin/acme-studio'"));
        Assert.That(script, Does.Not.Contain("'/usr/bin/agent-up'"));
    }

    [Test]
    public async Task ValidateInstalledStateAsync_withCustomManifest_looksUpProductCliNameFromFreshShell()
    {
        var files = new RecordingUbuntuFileSystem();
        files.ExistingFiles.Add("/usr/share/applications/acme-studio.desktop");
        var commands = new RecordingCommandRunner();
        var adapter = CustomAdapter(commands, files);

        await adapter.ValidateInstalledStateAsync(CustomSession());

        Assert.That(commands.Commands, Does.Contain(("bash", "-lc command -v \"$1\" -- acme-studio")));
        Assert.That(commands.Commands.Select(c => c.Arguments), Does.Not.Contain("-lc command -v \"$1\" -- agent-up"));
    }

    // ── Test 3: payload installed under the product's own install root ────────

    [Test]
    public async Task ExecuteInstallAsync_withCustomManifest_installsUnderProductRootNotAgentUpRoot()
    {
        var commands = new ScriptCapturingCommandRunner();
        var adapter = CustomAdapter(commands, new RecordingUbuntuFileSystem());

        await adapter.ExecuteInstallAsync(CustomSession()).DrainAsync();

        var script = commands.CapturedScript;
        Assert.That(script, Does.Contain("'/opt/acme-studio/desktop'")
            .And.Contain("'/opt/acme-studio/server'")
            .And.Contain("'/opt/acme-studio/cli'"));
        Assert.That(script, Does.Not.Contain("'/opt/agent-up/"));
    }

    // ── Test 4: uninstall removes exactly the product's artifacts ─────────────

    [Test]
    public async Task ExecuteComponentActionAsync_uninstall_withCustomManifest_removesOnlyProductArtifacts()
    {
        var session = CustomSession();
        var serverCommands = new ScriptCapturingCommandRunner();
        var cliCommands = new ScriptCapturingCommandRunner();
        var desktopCommands = new ScriptCapturingCommandRunner();

        await CustomAdapter(serverCommands, new RecordingUbuntuFileSystem())
            .ExecuteComponentActionAsync(ProductComponent.Server, InstallerComponentAction.Uninstall, session).DrainAsync();
        await CustomAdapter(cliCommands, new RecordingUbuntuFileSystem())
            .ExecuteComponentActionAsync(ProductComponent.Cli, InstallerComponentAction.Uninstall, session).DrainAsync();
        await CustomAdapter(desktopCommands, new RecordingUbuntuFileSystem())
            .ExecuteComponentActionAsync(ProductComponent.Desktop, InstallerComponentAction.Uninstall, session).DrainAsync();

        var serverScript = serverCommands.CapturedScript ?? "";
        var cliScript = cliCommands.CapturedScript ?? "";
        var desktopScript = desktopCommands.CapturedScript ?? "";

        Assert.Multiple(() =>
        {
            Assert.That(serverScript, Does.Contain("acme-studio-server.service"));
            Assert.That(serverScript, Does.Contain("'/etc/systemd/system/acme-studio-server.service'"));
            Assert.That(serverScript, Does.Contain("'/opt/acme-studio/server'"));

            Assert.That(cliScript, Does.Contain("'/usr/bin/acme-studio'"));
            Assert.That(cliScript, Does.Contain("'/opt/acme-studio/cli'"));

            Assert.That(desktopScript, Does.Contain("'/usr/share/applications/acme-studio.desktop'"));
            Assert.That(desktopScript, Does.Contain("'/opt/acme-studio/desktop'"));

            var allScripts = serverScript + cliScript + desktopScript;
            Assert.That(allScripts, Does.Not.Contain("agent-up"),
                "Uninstall must not touch any Agent-Up paths");
        });
    }

    // ── Test 5: theory – two manifests have distinct identity and non-overlapping paths ──

    private static IEnumerable<TestCaseData> ManifestPairs()
    {
        yield return new TestCaseData(
                UbuntuInstallerManifest.AgentUp(),
                UbuntuInstallerManifest.ForProduct(new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")))
            .SetName("AgentUp_vs_AcmeStudio");
    }

    [TestCaseSource(nameof(ManifestPairs))]
    public void TwoUbuntuManifests_haveDistinctServiceUnitSymlinkPathAndInstallRoot(
        UbuntuInstallerManifest first, UbuntuInstallerManifest second)
    {
        var firstPaths = UbuntuInstallerPaths.ForProduct(first);
        var secondPaths = UbuntuInstallerPaths.ForProduct(second);

        Assert.Multiple(() =>
        {
            Assert.That(first.ServiceUnitName, Is.Not.EqualTo(second.ServiceUnitName), "Service unit names must differ");
            Assert.That(first.CliCommandName, Is.Not.EqualTo(second.CliCommandName), "CLI command names must differ");
            Assert.That(firstPaths.RootDirectory, Is.Not.EqualTo(secondPaths.RootDirectory), "Install roots must differ");
            Assert.That(firstPaths.ServicePath, Is.Not.EqualTo(secondPaths.ServicePath), "Service file paths must differ");
            Assert.That(firstPaths.CliSymlinkPath, Is.Not.EqualTo(secondPaths.CliSymlinkPath), "CLI symlink paths must differ");
            Assert.That(firstPaths.RootDirectory, Does.Not.Contain(second.PackageName),
                $"'{first.PackageName}' root must not contain '{second.PackageName}' slug");
            Assert.That(secondPaths.RootDirectory, Does.Not.Contain(first.PackageName),
                $"'{second.PackageName}' root must not contain '{first.PackageName}' slug");
        });
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static ProductManifest AcmeStudioProduct => new("Acme Studio", "acme-studio", "ACMESTUDIO")
    {
        Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
    };

    private static InstallerSession Session()
        => InstallerSession.CreateDefault(ProductManifest.AgentUp(), new Version(1, 2, 3), "/opt/agent-up", PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static InstallerSession CustomSession()
    {
        var manifest = UbuntuInstallerManifest.ForProduct(AcmeStudioProduct);
        var paths = UbuntuInstallerPaths.ForProduct(manifest);
        return InstallerSession.CreateDefault(AcmeStudioProduct, new Version(1, 2, 3), paths.RootDirectory, PayloadSelection.Bundled(new Version(1, 2, 3)));
    }

    private static UbuntuInstallerOptions Options()
        => new(
            new UbuntuInstallPayload(
                "/payload/desktop",
                "/payload/server",
                "/payload/cli",
                "/payload/tray",
                "/payload/agent-up-server.service",
                "/payload/logo.png"),
            UbuntuInstallerPaths.SystemDefault(),
            UbuntuInstallerManifest.AgentUp());

    private static UbuntuInstallerOptions CustomOptions()
    {
        var manifest = UbuntuInstallerManifest.ForProduct(AcmeStudioProduct);
        var paths = UbuntuInstallerPaths.ForProduct(manifest);
        return new(
            new UbuntuInstallPayload(
                "/payload/desktop",
                "/payload/server",
                "/payload/cli",
                "/payload/tray",
                "/payload/acme-studio-server.service",
                "/payload/logo.png"),
            paths,
            manifest);
    }

    private static UbuntuInstallerPlatformAdapter Adapter(
        ICommandRunner commands,
        RecordingUbuntuFileSystem files)
        => new(
            commands,
            files,
            Options(),
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));

    private static UbuntuInstallerPlatformAdapter CustomAdapter(
        ICommandRunner commands,
        RecordingUbuntuFileSystem files)
        => new(
            commands,
            files,
            CustomOptions(),
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));

    // Reads the temp script file written by RunElevatedAsync before the command is invoked.
    private sealed class ScriptCapturingCommandRunner : ICommandRunner
    {
        public List<(string FileName, string Arguments)> PlainCommands { get; } = [];
        public string? CapturedScript { get; private set; }

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            PlainCommands.Add((fileName, string.Join(" ", arguments)));

            var scriptPath = ExtractScriptPath(fileName, arguments);
            if (scriptPath is not null && IsUnderTemp(scriptPath) && File.Exists(scriptPath))
                CapturedScript = (CapturedScript ?? "") + File.ReadAllText(scriptPath);

            return Task.FromResult(new ProcessResult(0, "", ""));
        }

        private static string? ExtractScriptPath(string fileName, IReadOnlyList<string> arguments)
        {
            // bash <path>  (already root — Environment.IsPrivilegedProcess)
            if (fileName == "bash" && arguments.Count == 1 && !arguments[0].StartsWith("-", StringComparison.Ordinal))
                return arguments[0];
            // pkexec bash <path>  (non-root elevation)
            if (fileName == "pkexec" && arguments.Count == 2 && arguments[0] == "bash")
                return arguments[1];
            return null;
        }

        private static bool IsUnderTemp(string path)
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(tempRoot, StringComparison.Ordinal);
        }
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<(string FileName, string Arguments)> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            Commands.Add((fileName, string.Join(" ", arguments)));
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    private sealed class RecordingUbuntuFileSystem : IUbuntuInstallerFileSystem
    {
        public HashSet<string> ExistingFiles { get; } = [];

        public void ResetDirectory(string path) { }
        public void DeleteDirectory(string path) { }
        public void DeleteFile(string path) { }
        public void CreateDirectory(string path) { }
        public void CopyDirectory(string source, string destination) { }
        public void CopyFile(string source, string destination) { }
        public void WriteText(string path, string text) => ExistingFiles.Add(path);
        public void CreateSymbolicLink(string path, string target) { }
        public void SetExecutable(string path) { }
        public bool FileExists(string path) => ExistingFiles.Contains(path);
    }
}
