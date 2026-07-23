using AgentUp.Installers.Features.Installation.Factories;
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
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
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
    public async Task ExecuteInstallAsync_appliesPayloadAndRegistersNativeUbuntuIntegration()
    {
        var files = new RecordingUbuntuFileSystem();
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
        Assert.That(files.ResetDirectories, Does.Contain("/opt/agent-up"));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/desktop", "/opt/agent-up/desktop")));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/server", "/opt/agent-up/server")));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/cli", "/opt/agent-up/cli")));
        Assert.That(files.CopiedFiles, Does.Contain(("/payload/agent-up-server.service", "/etc/systemd/system/agent-up-server.service")));
        Assert.That(files.Symlinks, Does.Contain(("/usr/bin/agent-up", "/opt/agent-up/cli/AgentUp.CLI")));
        Assert.That(files.Writes["/usr/share/applications/agent-up.desktop"], Does.Contain("Exec=/opt/agent-up/desktop/AgentUp.Desktop"));
        Assert.That(commands.Commands, Does.Contain(("systemctl", "daemon-reload")));
        Assert.That(commands.Commands, Does.Contain(("systemctl", "enable --now agent-up-server.service")));
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
            InstallOperationKind.RegisterUninstall
        }));
    }

    // ── Test 1: non-Agent-Up install registers the product's systemd unit ────

    [Test]
    public async Task ExecuteInstallAsync_withCustomManifest_registersProductServiceUnitNotAgentUpUnit()
    {
        var files = new RecordingUbuntuFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = CustomAdapter(commands, files);

        await adapter.ExecuteInstallAsync(CustomSession()).DrainAsync();

        Assert.That(commands.Commands, Does.Contain(("systemctl", "enable --now acme-studio-server.service")));
        Assert.That(commands.Commands, Does.Not.Contain(("systemctl", "enable --now agent-up-server.service")));
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
        var files = new RecordingUbuntuFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = CustomAdapter(commands, files);

        await adapter.ExecuteInstallAsync(CustomSession()).DrainAsync();

        Assert.That(files.Symlinks, Does.Contain(("/usr/bin/acme-studio", "/opt/acme-studio/cli/AgentUp.CLI")));
        Assert.That(files.Symlinks.Select(s => s.Path), Does.Not.Contain("/usr/bin/agent-up"));
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
        var files = new RecordingUbuntuFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = CustomAdapter(commands, files);

        await adapter.ExecuteInstallAsync(CustomSession()).DrainAsync();

        Assert.That(files.ResetDirectories, Does.Contain("/opt/acme-studio"));
        Assert.That(files.ResetDirectories, Does.Not.Contain("/opt/agent-up"));
        Assert.That(files.CopiedDirectories.Select(d => d.Destination),
            Does.Not.Contain("/opt/agent-up/desktop")
            .And.Not.Contain("/opt/agent-up/server")
            .And.Not.Contain("/opt/agent-up/cli"));
    }

    // ── Test 4: uninstall removes exactly the product's artifacts ─────────────

    [Test]
    public async Task ExecuteComponentActionAsync_uninstall_withCustomManifest_removesOnlyProductArtifacts()
    {
        var files = new RecordingUbuntuFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = CustomAdapter(commands, files);
        var session = CustomSession();

        await adapter.ExecuteComponentActionAsync(ProductComponent.Server, InstallerComponentAction.Uninstall, session).DrainAsync();
        await adapter.ExecuteComponentActionAsync(ProductComponent.Cli, InstallerComponentAction.Uninstall, session).DrainAsync();
        await adapter.ExecuteComponentActionAsync(ProductComponent.Desktop, InstallerComponentAction.Uninstall, session).DrainAsync();

        Assert.Multiple(() =>
        {
            // Removes the product's systemd unit
            Assert.That(commands.Commands, Does.Contain(("systemctl", "disable --now acme-studio-server.service")));
            Assert.That(files.DeletedFiles, Does.Contain("/etc/systemd/system/acme-studio-server.service"));
            // Removes the product's CLI symlink
            Assert.That(files.DeletedFiles, Does.Contain("/usr/bin/acme-studio"));
            // Removes the product's desktop entry
            Assert.That(files.DeletedFiles, Does.Contain("/usr/share/applications/acme-studio.desktop"));

            // Does NOT touch Agent-Up paths
            Assert.That(commands.Commands.Select(c => c.Arguments),
                Does.Not.Contain("disable --now agent-up-server.service"));
            Assert.That(files.DeletedFiles, Does.Not.Contain("/etc/systemd/system/agent-up-server.service"));
            Assert.That(files.DeletedFiles, Does.Not.Contain("/usr/bin/agent-up"));
            Assert.That(files.DeletedFiles, Does.Not.Contain("/usr/share/applications/agent-up.desktop"));
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
                "/payload/acme-studio-server.service",
                "/payload/logo.png"),
            paths,
            manifest);
    }

    private static UbuntuInstallerPlatformAdapter Adapter(
        RecordingCommandRunner commands,
        RecordingUbuntuFileSystem files)
        => new(
            commands,
            files,
            Options(),
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));

    private static UbuntuInstallerPlatformAdapter CustomAdapter(
        RecordingCommandRunner commands,
        RecordingUbuntuFileSystem files)
        => new(
            commands,
            files,
            CustomOptions(),
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));

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
        public List<string> ResetDirectories { get; } = [];
        public List<string> CreatedDirectories { get; } = [];
        public List<string> DeletedDirectories { get; } = [];
        public List<string> DeletedFiles { get; } = [];
        public List<(string Source, string Destination)> CopiedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedFiles { get; } = [];
        public Dictionary<string, string> Writes { get; } = [];
        public List<(string Path, string Target)> Symlinks { get; } = [];
        public List<string> Executables { get; } = [];
        public HashSet<string> ExistingFiles { get; } = [];

        public void ResetDirectory(string path) => ResetDirectories.Add(path);
        public void DeleteDirectory(string path) => DeletedDirectories.Add(path);
        public void DeleteFile(string path) => DeletedFiles.Add(path);
        public void CreateDirectory(string path) => CreatedDirectories.Add(path);
        public void CopyDirectory(string source, string destination) => CopiedDirectories.Add((source, destination));
        public void CopyFile(string source, string destination) => CopiedFiles.Add((source, destination));
        public void WriteText(string path, string text)
        {
            Writes[path] = text;
            ExistingFiles.Add(path);
        }
        public void CreateSymbolicLink(string path, string target) => Symlinks.Add((path, target));
        public void SetExecutable(string path) => Executables.Add(path);
        public bool FileExists(string path) => ExistingFiles.Contains(path);
    }
}
