using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Execution.Models;
using AgentUp.Installers.Features.Flow;
using AgentUp.Installers.Features.Flow.Models;
using AgentUp.Installers.Features.MacOs;
using AgentUp.Installers.Features.MacOs.DTOs;
using AgentUp.Installers.Features.MacOs.Models;
using AgentUp.Installers.Features.MacOs.Providers;
using AgentUp.Installers.Features.Payloads;
using AgentUp.Installers.Features.Payloads.Models;
using AgentUp.Installers.Features.Prerequisites;
using AgentUp.Installers.Features.Prerequisites.Services;

namespace AgentUp.Installers.Tests.Features.MacOs;

[TestFixture]
public class MacOsInstallerPlatformAdapterTests
{
    [Test]
    public async Task ExecuteInstallAsync_appliesPayloadAndRegistersLaunchdAndCliIntegration()
    {
        var files = new RecordingMacOsFileSystem();
        var commands = new RecordingCommandRunner();
        var adapter = new MacOsInstallerPlatformAdapter(commands, files, Options());
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
        Assert.That(files.ResetDirectories, Does.Contain("/Applications/Agent-Up.app"));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/desktop", "/Applications/Agent-Up.app/Contents/MacOS")));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/server", "/Library/Application Support/Agent-Up/server")));
        Assert.That(files.CopiedDirectories, Does.Contain(("/payload/cli", "/usr/local/agent-up/cli")));
        Assert.That(files.Writes["/Library/LaunchDaemons/dev.agent-up.server.plist"], Does.Contain("/Library/Application Support/Agent-Up/server/AgentUp.Server"));
        Assert.That(files.Symlinks, Does.Contain(("/usr/local/bin/agent-up", "/usr/local/agent-up/cli/AgentUp.CLI")));
        Assert.That(commands.Commands, Does.Contain(("launchctl", "bootstrap system \"/Library/LaunchDaemons/dev.agent-up.server.plist\"")));
        Assert.That(commands.Commands, Does.Contain(("launchctl", "kickstart -k system/dev.agent-up.server")));
    }

    [Test]
    public async Task ValidateInstalledStateAsync_reportsSuccessFromMacOsState()
    {
        var files = new RecordingMacOsFileSystem();
        files.ExistingFiles.Add("/Applications/Agent-Up.app/Contents/Info.plist");
        var commands = new RecordingCommandRunner();
        var adapter = new MacOsInstallerPlatformAdapter(commands, files, Options());

        var report = await adapter.ValidateInstalledStateAsync(Session());

        Assert.That(report.Succeeded, Is.True);
        Assert.That(commands.Commands, Does.Contain(("launchctl", "print system/dev.agent-up.server")));
        Assert.That(commands.Commands, Does.Contain(("bash", "-lc \"command -v agent-up\"")));
    }

    private static InstallerSession Session()
        => InstallerSession.CreateDefault("Agent-Up", new Version(1, 2, 3), "/Applications/Agent-Up.app", PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static MacOsInstallerOptions Options()
        => new(
            new MacOsInstallPayload("/payload/desktop", "/payload/server", "/payload/cli"),
            MacOsInstallerPaths.SystemDefault());

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<(string FileName, string Arguments)> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            Commands.Add((fileName, arguments));
            return Task.FromResult(new ProcessResult(0, "", ""));
        }
    }

    private sealed class RecordingMacOsFileSystem : IMacOsInstallerFileSystem
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
