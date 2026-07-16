using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
using AgentUp.Installers.Features.UbuntuInstallation;
using AgentUp.Installers.Features.UbuntuInstallation.DTOs;
using AgentUp.Installers.Features.UbuntuInstallation.Models;
using AgentUp.Installers.Features.UbuntuInstallation.Providers;

namespace AgentUp.Installers.Tests.Features.UbuntuInstallation;

[TestFixture]
public class UbuntuInstallerPlatformAdapterTests
{
    [Test]
    public async Task ExecuteInstallAsync_appliesPayloadAndRegistersNativeUbuntuIntegration()
    {
        var files = new RecordingUbuntuFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = new UbuntuInstallerPlatformAdapter(commands, files, Options());
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
        var adapter = new UbuntuInstallerPlatformAdapter(commands, files, Options());

        var report = await adapter.ValidateInstalledStateAsync(Session());

        Assert.That(report.Succeeded, Is.True);
        Assert.That(commands.Commands, Does.Contain(("systemctl", "is-enabled agent-up-server.service")));
        Assert.That(commands.Commands, Does.Contain(("systemctl", "is-active agent-up-server.service")));
        Assert.That(commands.Commands, Does.Contain(("bash", "-lc \"command -v agent-up\"")));
    }

    [Test]
    public void PlanInstall_marksNativeUbuntuOperationsAsElevationRequired()
    {
        var adapter = new UbuntuInstallerPlatformAdapter(new RecordingCommandRunner(), new RecordingUbuntuFileSystem(), Options());

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

    private static InstallerSession Session()
        => InstallerSession.CreateDefault("Agent-Up", new Version(1, 2, 3), "/opt/agent-up", PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static UbuntuInstallerOptions Options()
        => new(
            new UbuntuInstallPayload(
                "/payload/desktop",
                "/payload/server",
                "/payload/cli",
                "/payload/agent-up-server.service",
                "/payload/logo.png"),
            UbuntuInstallerPaths.SystemDefault());

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<(string FileName, string Arguments)> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            Commands.Add((fileName, arguments));
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    private sealed class RecordingUbuntuFileSystem : IUbuntuInstallerFileSystem
    {
        public List<string> ResetDirectories { get; } = [];
        public List<string> CreatedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedDirectories { get; } = [];
        public List<(string Source, string Destination)> CopiedFiles { get; } = [];
        public Dictionary<string, string> Writes { get; } = [];
        public List<(string Path, string Target)> Symlinks { get; } = [];
        public List<string> Executables { get; } = [];
        public HashSet<string> ExistingFiles { get; } = [];

        public void ResetDirectory(string path) => ResetDirectories.Add(path);
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
