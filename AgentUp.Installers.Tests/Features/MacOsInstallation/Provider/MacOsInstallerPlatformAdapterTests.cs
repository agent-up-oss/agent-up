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
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Tests.Features.MacOsInstallation.Provider;

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

        await adapter.ExecuteInstallAsync(Session()).DrainAsync();

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

        await adapter.ExecuteInstallAsync(Session()).DrainAsync();

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

        await adapter.ExecuteUninstallAsync(InstallerComponentTarget.Cli, Session()).DrainAsync();

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

        await adapter.ExecuteUninstallAsync(InstallerComponentTarget.Desktop, Session()).DrainAsync();

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
        Assert.That(commands.Commands, Does.Contain(("bash", "-lc command -v \"$1\" -- agent-up")));
    }

    [Test]
    public void ValidateInstalledStateAsync_withUnsafeManifestIdentity_throwsBeforeRunningCommands()
    {
        var commands = new RecordingCommandRunner();
        var adapter = Adapter(commands, new RecordingMacOsFileSystem());
        var manifest = new ProductManifest("Acme Studio", "acme;open", "ACMESTUDIO");
        var session = InstallerSession.CreateDefault(
            manifest,
            new Version(1, 2, 3),
            "/Applications/Acme Studio.app",
            PayloadSelection.Bundled(manifest.ProductName, new Version(1, 2, 3)));

        Assert.That(async () => await adapter.ValidateInstalledStateAsync(session), Throws.ArgumentException);
        Assert.That(commands.Commands, Is.Empty);
    }

    [Test]
    public async Task Install_withNonAgentUpManifest_registersProductLaunchdLabel_notAgentUpLabel()
    {
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, new RecordingMacOsFileSystem());
        var session = AcmeSession();

        await adapter.ExecuteInstallAsync(session).DrainAsync();
        await adapter.ValidateInstalledStateAsync(session);

        var script = commands.CapturedScript;
        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("dev.acme-studio.server"), "Install script must reference the product's launchd label");
            Assert.That(script, Does.Contain("launchctl bootstrap system '/Library/LaunchDaemons/dev.acme-studio.server.plist'"));
            Assert.That(script, Does.Contain("launchctl kickstart -k system/dev.acme-studio.server"));
            Assert.That(commands.PlainCommands, Does.Contain(("launchctl", "print system/dev.acme-studio.server")),
                "Validation must query the product's launchd service, not Agent-Up's");
            Assert.That(script, Does.Not.Contain("dev.agent-up.server"), "Agent-Up's launchd label must not appear in a non-Agent-Up install");
            Assert.That(commands.PlainCommands, Does.Not.Contain(("launchctl", "print system/dev.agent-up.server")),
                "Agent-Up's launchd label must not be queried");
        });
    }

    [Test]
    public async Task Install_withNonAgentUpManifest_placesAppBundleUnderProductName_allowingCoexistence()
    {
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, new RecordingMacOsFileSystem());

        await adapter.ExecuteInstallAsync(AcmeSession()).DrainAsync();

        var script = commands.CapturedScript;
        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("'/Applications/Acme Studio.app'"), "App bundle must be placed under the product name");
            Assert.That(script, Does.Not.Contain("Agent-Up.app"), "Agent-Up bundle must not be mentioned");
            Assert.That(AcmePaths().AppBundleDirectory, Is.Not.EqualTo(MacOsInstallerPaths.SystemDefault().AppBundleDirectory),
                "The two products must have distinct bundle directories");
        });
    }

    [Test]
    public async Task Install_withNonAgentUpManifest_createsCliSymlinkWithProductSlug()
    {
        var commands = new ScriptCapturingCommandRunner();
        var adapter = Adapter(commands, new RecordingMacOsFileSystem());
        var session = AcmeSession();

        await adapter.ExecuteInstallAsync(session).DrainAsync();
        await adapter.ValidateInstalledStateAsync(session);

        var script = commands.CapturedScript;
        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("'/usr/local/bin/acme-studio'"), "CLI symlink must use the product's slug");
            Assert.That(commands.PlainCommands, Does.Contain(("bash", "-lc command -v \"$1\" -- acme-studio")),
                "Shell validation must check the product's CLI name");
            Assert.That(script, Does.Not.Contain("'/usr/local/bin/agent-up'"), "Agent-Up CLI symlink must not be created");
        });
    }

    [Test]
    public async Task Uninstall_withNonAgentUpManifest_removesOnlyProductEntries_withoutTouchingAgentUp()
    {
        var session = AcmeSession();
        var serverCommands = new ScriptCapturingCommandRunner();
        var cliCommands = new ScriptCapturingCommandRunner();
        var desktopCommands = new ScriptCapturingCommandRunner();

        await Adapter(serverCommands, new RecordingMacOsFileSystem())
            .ExecuteUninstallAsync(InstallerComponentTarget.Server, session).DrainAsync();
        await Adapter(cliCommands, new RecordingMacOsFileSystem())
            .ExecuteUninstallAsync(InstallerComponentTarget.Cli, session).DrainAsync();
        await Adapter(desktopCommands, new RecordingMacOsFileSystem())
            .ExecuteUninstallAsync(InstallerComponentTarget.Desktop, session).DrainAsync();

        var serverScript = serverCommands.CapturedScript ?? "";
        var cliScript = cliCommands.CapturedScript ?? "";
        var desktopScript = desktopCommands.CapturedScript ?? "";
        var allScripts = serverScript + cliScript + desktopScript;

        Assert.Multiple(() =>
        {
            Assert.That(serverScript, Does.Contain("'/Library/LaunchDaemons/dev.acme-studio.server.plist'"));
            Assert.That(serverScript, Does.Contain("'/Library/Application Support/Acme Studio/server'"));
            Assert.That(serverScript, Does.Contain("'/usr/local/bin/acme-studio-server'"));
            Assert.That(cliScript, Does.Contain("'/usr/local/acme-studio/cli'"));
            Assert.That(cliScript, Does.Contain("'/usr/local/bin/acme-studio'"));
            Assert.That(desktopScript, Does.Contain("'/Applications/Acme Studio.app'"));
            Assert.That(desktopScript, Does.Contain("'/usr/local/bin/acme-studio-desktop'"));
            Assert.That(allScripts, Does.Not.Contain("agent-up").And.Not.Contain("Agent-Up"),
                "Uninstall must not touch any Agent-Up entries");
        });
    }

    private static IEnumerable<TestCaseData> ManifestPairs()
    {
        yield return new TestCaseData(ProductManifest.AgentUp(), AcmeStudio)
            .SetName("AgentUp_vs_AcmeStudio");
    }

    [TestCaseSource(nameof(ManifestPairs))]
    public async Task TwoManifests_haveDistinctMacOsIdentities_andBothInstallsCoexist(ProductManifest first, ProductManifest second)
    {
        var firstPaths = MacOsInstallerPaths.From(first);
        var secondPaths = MacOsInstallerPaths.From(second);
        var firstCommands = new ScriptCapturingCommandRunner();
        var secondCommands = new ScriptCapturingCommandRunner();

        await Adapter(firstCommands, new RecordingMacOsFileSystem())
            .ExecuteInstallAsync(SessionFor(first)).DrainAsync();
        await Adapter(secondCommands, new RecordingMacOsFileSystem())
            .ExecuteInstallAsync(SessionFor(second)).DrainAsync();

        var firstScript = firstCommands.CapturedScript ?? "";
        var secondScript = secondCommands.CapturedScript ?? "";

        Assert.Multiple(() =>
        {
            Assert.That(firstPaths.LaunchDaemonPath, Is.Not.EqualTo(secondPaths.LaunchDaemonPath), "launchd plist paths must differ");
            Assert.That(firstPaths.AppBundleDirectory, Is.Not.EqualTo(secondPaths.AppBundleDirectory), "App bundle paths must differ");
            Assert.That(firstPaths.CliSymlinkPath, Is.Not.EqualTo(secondPaths.CliSymlinkPath), "CLI symlink paths must differ");

            Assert.That(firstScript, Does.Contain($"dev.{first.Slug}.server"));
            Assert.That(secondScript, Does.Contain($"dev.{second.Slug}.server"));
            Assert.That(firstScript, Does.Contain($"'{firstPaths.AppBundleDirectory}'"));
            Assert.That(secondScript, Does.Contain($"'{secondPaths.AppBundleDirectory}'"));
            Assert.That(firstScript, Does.Contain($"'{firstPaths.CliSymlinkPath}'"));
            Assert.That(secondScript, Does.Contain($"'{secondPaths.CliSymlinkPath}'"));

            Assert.That(firstScript, Does.Not.Contain($"'{secondPaths.AppBundleDirectory}'"), "First install must not touch second product's bundle");
            Assert.That(secondScript, Does.Not.Contain($"'{firstPaths.AppBundleDirectory}'"), "Second install must not touch first product's bundle");
            Assert.That(firstScript, Does.Not.Contain($"'{secondPaths.LaunchDaemonPath}'"), "First install must not register second product's launchd service");
            Assert.That(secondScript, Does.Not.Contain($"'{firstPaths.LaunchDaemonPath}'"), "Second install must not register first product's launchd service");
        });
    }

    private static ProductManifest AcmeStudio
        => new("Acme Studio", "acme-studio", "ACMESTUDIO")
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
        };

    [TestCase("Acme Studio", "../acme")]
    [TestCase("../Acme Studio", "acme-studio")]
    public void MacOsInstallerPaths_From_withUnsafeProductIdentity_throwsArgumentException(
        string productName,
        string slug)
    {
        var manifest = new ProductManifest(productName, slug, "ACMESTUDIO");

        Assert.That(() => MacOsInstallerPaths.From(manifest), Throws.ArgumentException);
    }

    private static MacOsInstallerPaths AcmePaths()
        => MacOsInstallerPaths.From(AcmeStudio);

    private static InstallerSession AcmeSession()
        => SessionFor(AcmeStudio);

    private static InstallerSession SessionFor(ProductManifest manifest)
    {
        var paths = MacOsInstallerPaths.From(manifest);
        return InstallerSession.CreateDefault(
            manifest,
            new Version(1, 2, 3),
            paths.AppBundleDirectory,
            PayloadSelection.Bundled(manifest.ProductName, new Version(1, 2, 3)));
    }

    private static InstallerSession Session()
        => InstallerSession.CreateDefault(ProductManifest.AgentUp(), new Version(1, 2, 3), "/Applications/Agent-Up.app", PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static MacOsInstallerOptions Options()
        => new(new MacOsInstallPayload("/payload/desktop", "/payload/server", "/payload/cli", "/payload/icon/Agent-Up.png"));

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

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            PlainCommands.Add((fileName, string.Join(" ", arguments)));

            if (fileName is "osascript" or "bash")
            {
                var scriptPath = ExtractBashScriptPath(fileName, arguments);
                var safeScriptPath = SafeScriptPath(scriptPath);
                if (safeScriptPath is not null && File.Exists(safeScriptPath))
                    CapturedScript = (CapturedScript ?? "") + File.ReadAllText(safeScriptPath);
            }

            return Task.FromResult(new ProcessResult(0, "", ""));
        }

        private static string? ExtractBashScriptPath(string fileName, IReadOnlyList<string> arguments)
        {
            if (fileName == "bash" && arguments.Count == 1 && !arguments[0].StartsWith("-", StringComparison.Ordinal))
            {
                // bash: /tmp/xxx  (path passed unquoted — Process.Start doesn't process shell quotes)
                return arguments[0];
            }
            var safeArguments = arguments.Count == 1 ? SafeScriptPath(arguments[0]) : null;
            if (fileName == "osascript" && safeArguments is not null && File.Exists(safeArguments))
            {
                // osascript: /tmp/xxx.scpt  (AppleScript file containing the bash script path)
                var appleScript = File.ReadAllText(safeArguments);
                return ExtractDoShellScriptPath(appleScript);
            }
            return null;
        }

        private static string? SafeScriptPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(tempRoot, StringComparison.Ordinal) ? fullPath : null;
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

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            Commands.Add((fileName, string.Join(" ", arguments)));
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
