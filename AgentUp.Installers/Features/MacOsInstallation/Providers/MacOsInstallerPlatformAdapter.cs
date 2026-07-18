using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.MacOsInstallation.DTOs;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Features.MacOsInstallation.Providers;

public sealed class MacOsInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    private readonly ICommandRunner _commands;
    private readonly IMacOsInstallerFileSystem _files;
    private readonly MacOsInstallerOptions _options;
    private readonly IRequiredCommandRunner _requiredCommands;
    private readonly DockerPrerequisite _dockerPrerequisite;

    public MacOsInstallerPlatformAdapter(
        ICommandRunner commands,
        IMacOsInstallerFileSystem files,
        MacOsInstallerOptions options,
        IRequiredCommandRunner requiredCommands,
        DockerPrerequisite dockerPrerequisite)
    {
        _commands = commands;
        _files = files;
        _options = options;
        _requiredCommands = requiredCommands;
        _dockerPrerequisite = dockerPrerequisite;
    }

    public string PlatformName => "macOS";

    public bool SupportsInstallActions => true;

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await _dockerPrerequisite.CheckAsync(cancellationToken);

    public async Task<InstallerComponentStatus> GetComponentStatusAsync(
        InstallerComponentTarget target,
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => InstallerComponentOperations.StatusFromValidation(
            target,
            await ValidateInstalledStateAsync(session, cancellationToken),
            session.Version);

    public IReadOnlyList<InstallOperation> PlanComponentAction(
        InstallerComponentTarget target,
        InstallerComponentAction action,
        InstallerSession session)
        => InstallerComponentOperations.Plan(target, action, session, PlanInstall);

    public IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
        InstallerComponentTarget target,
        InstallerComponentAction action,
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => InstallerComponentOperations.ExecuteInstallLikeAction(
            target,
            action,
            session,
            ExecuteInstallAsync,
            cancellationToken);

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
    {
        var summary = session.Summary();
        var operations = new List<InstallOperation>
        {
            new(InstallOperationKind.ValidatePrerequisites, "Validate Docker and macOS prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Use {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, "Install selected Agent-Up files", true)
        };

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterService, "Register and start dev.agent-up.server launchd service", true));
        if (summary.Includes(InstallerComponent.Cli))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterCli, "Register /usr/local/bin Agent-Up commands", true));
        if (summary.Includes(InstallerComponent.Desktop))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterDesktop, "Register Agent-Up.app in /Applications", true));

        operations.Add(new InstallOperation(InstallOperationKind.RegisterUninstall, "Register native uninstall handoff", true));
        operations.Add(new InstallOperation(InstallOperationKind.ValidateInstallation, "Validate macOS installed state", false));
        return operations;
    }

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

        var summary = session.Summary();
        if (summary.Includes(InstallerComponent.Desktop))
        {
            _files.ResetDirectory(_options.Paths.AppBundleDirectory);
            _files.CreateDirectory(System.IO.Path.Join(_options.Paths.AppBundleDirectory, "Contents", "MacOS"));
            _files.CreateDirectory(_options.Paths.DesktopResourcesDirectory);
            _files.CopyDirectory(_options.Payload.DesktopDirectory, System.IO.Path.Join(_options.Paths.AppBundleDirectory, "Contents", "MacOS"));
            _files.CopyFile(_options.Payload.IconPath, _options.Paths.DesktopIconPath);
            _files.WriteText(_options.Paths.DesktopInfoPlistPath, plists.DesktopInfoPlist());
            _files.SetExecutable(_options.Paths.DesktopExecutable);
        }

        if (summary.Includes(InstallerComponent.Server))
        {
            _files.ResetDirectory(_options.Paths.ServerDirectory);
            _files.CopyDirectory(_options.Payload.ServerDirectory, _options.Paths.ServerDirectory);
            _files.WriteText(_options.Paths.LaunchDaemonPath, plists.LaunchDaemonPlist());
            _files.CreateDirectory(_options.Paths.ApplicationSupportDirectory);
            _files.CreateDirectory(_options.Paths.LogsDirectory);
            _files.SetExecutable(_options.Paths.ServerExecutable);
        }

        if (summary.Includes(InstallerComponent.Cli))
        {
            _files.ResetDirectory(_options.Paths.CliDirectory);
            _files.CopyDirectory(_options.Payload.CliDirectory, _options.Paths.CliDirectory);
            _files.SetExecutable(_options.Paths.CliExecutable);
        }
        yield return progress.Complete(InstallOperationKind.InstallFiles);

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
        {
            await _requiredCommands.RunAsync("chown", $"root:wheel \"{_options.Paths.LaunchDaemonPath}\"", cancellationToken);
            await _requiredCommands.RunAsync("chmod", $"644 \"{_options.Paths.LaunchDaemonPath}\"", cancellationToken);
            await _requiredCommands.RunAsync("launchctl", $"bootstrap system \"{_options.Paths.LaunchDaemonPath}\"", cancellationToken);
            await _requiredCommands.RunAsync("launchctl", $"kickstart -k system/{manifest.ServerLaunchDaemonLabel}", cancellationToken);
            yield return progress.Complete(InstallOperationKind.RegisterService);
        }

        if (summary.Includes(InstallerComponent.Cli))
        {
            _files.CreateSymbolicLink(_options.Paths.CliSymlinkPath, _options.Paths.CliExecutable);
            yield return progress.Complete(InstallOperationKind.RegisterCli);
        }

        if (summary.Includes(InstallerComponent.Server))
            _files.CreateSymbolicLink(_options.Paths.ServerSymlinkPath, _options.Paths.ServerExecutable);

        if (summary.Includes(InstallerComponent.Desktop))
            _files.CreateSymbolicLink(_options.Paths.DesktopSymlinkPath, _options.Paths.DesktopExecutable);

        if (summary.Includes(InstallerComponent.Desktop))
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
