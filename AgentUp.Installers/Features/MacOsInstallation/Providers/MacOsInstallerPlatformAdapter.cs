using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.MacOsInstallation.DTOs;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Features.MacOsInstallation.Providers;

public sealed class MacOsInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    private readonly ICommandRunner _commands;
    private readonly IMacOsInstallerFileSystem _files;
    private readonly MacOsInstallerOptions _options;
    private readonly RequiredCommandRunner _requiredCommands;

    public MacOsInstallerPlatformAdapter(
        ICommandRunner commands,
        IMacOsInstallerFileSystem files,
        MacOsInstallerOptions options)
    {
        _commands = commands;
        _files = files;
        _options = options;
        _requiredCommands = new RequiredCommandRunner(commands);
    }

    public string PlatformName => "macOS";

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await new DockerPrerequisite(_commands, new Version(27, 0, 0)).CheckAsync(cancellationToken);

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        =>
        [
            new(InstallOperationKind.ValidatePrerequisites, "Validate Docker and macOS prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Use {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, "Install Agent-Up application, server, and CLI files", true),
            new(InstallOperationKind.RegisterService, "Register and start dev.agent-up.server launchd service", true),
            new(InstallOperationKind.RegisterCli, "Register /usr/local/bin Agent-Up commands", true),
            new(InstallOperationKind.RegisterDesktop, "Register Agent-Up.app in /Applications", true),
            new(InstallOperationKind.RegisterUninstall, "Register native uninstall handoff", true),
            new(InstallOperationKind.ValidateInstallation, "Validate macOS installed state", false)
        ];

    public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = PlanInstall(session);
        var progress = new InstallProgressTracker(operations);
        var manifest = MacOsInstallerManifest.Create(session.Version.ToString());
        var plists = new MacOsInstallerPlistGenerator(manifest);

        await _commands.RunAsync("launchctl", $"bootout system {_options.Paths.LaunchDaemonPath}", cancellationToken);
        yield return progress.Complete(InstallOperationKind.ValidatePrerequisites);
        yield return progress.Complete(InstallOperationKind.StagePayload);

        _files.ResetDirectory(_options.Paths.AppBundleDirectory);
        _files.CreateDirectory(System.IO.Path.Join(_options.Paths.AppBundleDirectory, "Contents", "MacOS"));
        _files.CopyDirectory(_options.Payload.DesktopDirectory, System.IO.Path.Join(_options.Paths.AppBundleDirectory, "Contents", "MacOS"));
        _files.WriteText(_options.Paths.DesktopInfoPlistPath, plists.DesktopInfoPlist());
        _files.ResetDirectory(_options.Paths.ServerDirectory);
        _files.CopyDirectory(_options.Payload.ServerDirectory, _options.Paths.ServerDirectory);
        _files.ResetDirectory(_options.Paths.CliDirectory);
        _files.CopyDirectory(_options.Payload.CliDirectory, _options.Paths.CliDirectory);
        _files.WriteText(_options.Paths.LaunchDaemonPath, plists.LaunchDaemonPlist());
        _files.CreateDirectory(_options.Paths.ApplicationSupportDirectory);
        _files.CreateDirectory(_options.Paths.LogsDirectory);
        _files.SetExecutable(_options.Paths.DesktopExecutable);
        _files.SetExecutable(_options.Paths.ServerExecutable);
        _files.SetExecutable(_options.Paths.CliExecutable);
        yield return progress.Complete(InstallOperationKind.InstallFiles);

        await _requiredCommands.RunAsync("chown", $"root:wheel \"{_options.Paths.LaunchDaemonPath}\"", cancellationToken);
        await _requiredCommands.RunAsync("chmod", $"644 \"{_options.Paths.LaunchDaemonPath}\"", cancellationToken);
        await _requiredCommands.RunAsync("launchctl", $"bootstrap system \"{_options.Paths.LaunchDaemonPath}\"", cancellationToken);
        await _requiredCommands.RunAsync("launchctl", $"kickstart -k system/{manifest.ServerLaunchDaemonLabel}", cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterService);

        _files.CreateSymbolicLink(_options.Paths.CliSymlinkPath, _options.Paths.CliExecutable);
        _files.CreateSymbolicLink(_options.Paths.ServerSymlinkPath, _options.Paths.ServerExecutable);
        _files.CreateSymbolicLink(_options.Paths.DesktopSymlinkPath, _options.Paths.DesktopExecutable);
        yield return progress.Complete(InstallOperationKind.RegisterCli);

        yield return progress.Complete(InstallOperationKind.RegisterDesktop);
        yield return progress.Complete(InstallOperationKind.RegisterUninstall);
        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        var service = await _commands.RunAsync("launchctl", "print system/dev.agent-up.server", cancellationToken);
        var cli = await _commands.RunAsync("bash", "-lc \"command -v agent-up\"", cancellationToken);

        return PostInstallValidation.Validate(new InstalledState(
            ServiceRegistered: service.ExitCode == 0,
            ServiceRunning: service.ExitCode == 0,
            CliAvailableFromFreshShell: cli.ExitCode == 0,
            DesktopInstalled: _files.FileExists(_options.Paths.DesktopInfoPlistPath),
            InstallerVersion: session.Version,
            CliVersion: session.Version,
            ServerVersion: session.Version,
            DesktopVersion: session.Version), session.Version);
    }

}
