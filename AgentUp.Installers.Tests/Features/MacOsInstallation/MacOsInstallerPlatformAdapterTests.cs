using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.MacOsInstallation.DTOs;
using AgentUp.Installers.Features.MacOsInstallation.Models;
using AgentUp.Installers.Features.MacOsInstallation.Providers;
using AgentUp.Installers.Features.MacOsInstallation.Services;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Tests.Features.MacOsInstallation;

[TestFixture]
public class MacOsInstallerPlatformAdapterTests
{
    [Test]
    public async Task ExecuteInstallAsync_yieldsCorrectProgressSequence()
    {
        var files = new RecordingMacOsFileSystem();
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
    }

    [Test]
    public async Task ExecuteInstallAsync_runsElevatedScriptContainingAllComponents()
    {
        var files = new RecordingMacOsFileSystem();
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, files);

        await foreach (var progress in adapter.ExecuteInstallAsync(Session())) { _ = progress; }

        var script = commands.CapturedScript;
        Assert.That(script, Is.Not.Null.And.Not.Empty, "Elevated script was not executed.");

        // Desktop operations
        Assert.That(script, Does.Contain("rm -rf '/Applications/Agent-Up.app'"));
        Assert.That(script, Does.Contain("cp -r '/payload/desktop'/."));
        Assert.That(script, Does.Contain("cp '/payload/icon/Agent-Up.png'"));
        Assert.That(script, Does.Contain("chmod +x '/Applications/Agent-Up.app/Contents/MacOS/AgentUp.Desktop'"));
        Assert.That(script, Does.Contain("ln -sf '/Applications/Agent-Up.app/Contents/MacOS/AgentUp.Desktop' '/usr/local/bin/agent-up-desktop'"));

        // Server operations
        Assert.That(script, Does.Contain("rm -rf '/Library/Application Support/Agent-Up/server'"));
        Assert.That(script, Does.Contain("cp -r '/payload/server'/."));
        Assert.That(script, Does.Contain("chown root:wheel '/Library/LaunchDaemons/dev.agent-up.server.plist'"));
        Assert.That(script, Does.Contain("chmod 644 '/Library/LaunchDaemons/dev.agent-up.server.plist'"));
        Assert.That(script, Does.Contain("launchctl bootstrap system '/Library/LaunchDaemons/dev.agent-up.server.plist'"));
        Assert.That(script, Does.Contain("launchctl kickstart -k system/dev.agent-up.server"));
        Assert.That(script, Does.Contain("ln -sf '/Library/Application Support/Agent-Up/server/AgentUp.Server' '/usr/local/bin/agent-up-server'"));

        // CLI operations
        Assert.That(script, Does.Contain("rm -rf '/usr/local/agent-up/cli'"));
        Assert.That(script, Does.Contain("cp -r '/payload/cli'/."));
        Assert.That(script, Does.Contain("chmod +x '/usr/local/agent-up/cli/AgentUp.CLI'"));
        Assert.That(script, Does.Contain("ln -sf '/usr/local/agent-up/cli/AgentUp.CLI' '/usr/local/bin/agent-up'"));
    }

    [Test]
    public async Task ExecuteInstallAsync_bootsOutExistingServiceBeforeInstall()
    {
        var files = new RecordingMacOsFileSystem();
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, files);

        await foreach (var progress in adapter.ExecuteInstallAsync(Session())) { _ = progress; }

        Assert.That(commands.PlainCommands, Does.Contain(
            ("launchctl", "bootout system /Library/LaunchDaemons/dev.agent-up.server.plist")));
    }

    [Test]
    public async Task ExecuteUninstallAsync_server_removesServiceFilesAndSymlink()
    {
        var files = new RecordingMacOsFileSystem();
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, files);

        var progress = new List<InstallProgress>();
        await foreach (var item in adapter.ExecuteUninstallAsync(InstallerComponentTarget.Server, Session()))
            progress.Add(item);

        var script = commands.CapturedScript;
        Assert.That(script, Does.Contain("launchctl bootout system '/Library/LaunchDaemons/dev.agent-up.server.plist'"));
        Assert.That(script, Does.Contain("rm -f '/Library/LaunchDaemons/dev.agent-up.server.plist'"));
        Assert.That(script, Does.Contain("rm -rf '/Library/Application Support/Agent-Up/server'"));
        Assert.That(script, Does.Contain("rm -f '/usr/local/bin/agent-up-server'"));

        Assert.That(progress.Select(p => p.Kind), Is.EqualTo(new[]
        {
            InstallOperationKind.RegisterService,
            InstallOperationKind.ValidateInstallation
        }));
    }

    [Test]
    public async Task ExecuteUninstallAsync_cli_removesCliFilesAndSymlink()
    {
        var files = new RecordingMacOsFileSystem();
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, files);

        await foreach (var progress in adapter.ExecuteUninstallAsync(InstallerComponentTarget.Cli, Session())) { _ = progress; }

        var script = commands.CapturedScript;
        Assert.That(script, Does.Contain("rm -rf '/usr/local/agent-up/cli'"));
        Assert.That(script, Does.Contain("rm -f '/usr/local/bin/agent-up'"));
    }

    [Test]
    public async Task ExecuteUninstallAsync_desktop_removesAppBundleAndSymlink()
    {
        var files = new RecordingMacOsFileSystem();
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, files);

        await foreach (var progress in adapter.ExecuteUninstallAsync(InstallerComponentTarget.Desktop, Session())) { _ = progress; }

        var script = commands.CapturedScript;
        Assert.That(script, Does.Contain("rm -rf '/Applications/Agent-Up.app'"));
        Assert.That(script, Does.Contain("rm -f '/usr/local/bin/agent-up-desktop'"));
    }

    [Test]
    public async Task ValidateInstalledStateAsync_reportsSuccessFromMacOsState()
    {
        var files = new RecordingMacOsFileSystem();
        files.ExistingFiles.Add("/Applications/Agent-Up.app/Contents/Info.plist");
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, files);

        var report = await adapter.ValidateInstalledStateAsync(Session());

        Assert.That(report.Succeeded, Is.True);
        Assert.That(commands.Commands, Does.Contain(("launchctl", "print system/dev.agent-up.server")));
        Assert.That(commands.Commands, Does.Contain(("bash", "-lc \"command -v agent-up\"")));
    }

    [Test]
    public void InstallerPostInstallScript_onlyOpensGui()
    {
        var script = MacOsInstallerScripts.InstallerPostInstallScript();

        Assert.That(script, Does.Contain("open -a \"/Applications/Agent-Up Installer.app\""));
        Assert.That(script, Does.Not.Contain("--install-core"));
    }

    private static InstallerSession Session()
        => InstallerSession.CreateDefault(ProductManifest.AgentUp(), new Version(1, 2, 3), "/Applications/Agent-Up.app", PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static MacOsInstallerOptions Options()
        => new(
            new MacOsInstallPayload("/payload/desktop", "/payload/server", "/payload/cli", "/payload/icon/Agent-Up.png"),
            MacOsInstallerPaths.SystemDefault());

    private static MacOsInstallerPlatformAdapter Adapter(
        ICommandRunner commands,
        RecordingMacOsFileSystem files)
        => new(
            commands,
            files,
            Options(),
            new RequiredCommandRunner(commands),
            new DockerPrerequisite(new DockerPrerequisiteProvider(commands), new Version(27, 0, 0)));

    // Reads the temp script file written by RunElevatedAsync before the command is invoked.
    private sealed class ScriptCapturingCommandRunner : ICommandRunner
    {
        public List<(string FileName, string Arguments)> PlainCommands { get; } = [];
        public string? CapturedScript { get; private set; }

        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            PlainCommands.Add((fileName, arguments));

            if (fileName is "osascript" or "bash")
            {
                var scriptPath = ExtractBashScriptPath(fileName, arguments);
                if (scriptPath is not null && File.Exists(scriptPath))
                    CapturedScript = (CapturedScript ?? "") + File.ReadAllText(scriptPath);
            }

            return Task.FromResult(new ProcessResult(0, "", ""));
        }

        private static string? ExtractBashScriptPath(string fileName, string arguments)
        {
            if (fileName == "bash" && !arguments.StartsWith("-", StringComparison.Ordinal))
            {
                // bash: /tmp/xxx  (path passed unquoted — Process.Start doesn't process shell quotes)
                return arguments;
            }
            if (fileName == "osascript" && File.Exists(arguments))
            {
                // osascript: /tmp/xxx.scpt  (AppleScript file containing the bash script path)
                var appleScript = File.ReadAllText(arguments);
                return ExtractDoShellScriptPath(appleScript);
            }
            return null;
        }

        private static string? ExtractDoShellScriptPath(string appleScript)
        {
            var start = appleScript.IndexOf("do shell script \"", StringComparison.Ordinal);
            if (start < 0) return null;
            start += "do shell script \"".Length;
            var end = appleScript.IndexOf('"', start);
            return end > start ? appleScript[start..end] : null;
        }
    }

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
