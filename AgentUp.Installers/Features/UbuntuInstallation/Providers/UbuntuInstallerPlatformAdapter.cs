using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
using AgentUp.Installers.Features.UbuntuInstallation.DTOs;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Models;

namespace AgentUp.Installers.Features.UbuntuInstallation.Providers;

public sealed class UbuntuInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    private readonly ICommandRunner _commands;
    private readonly IUbuntuInstallerFileSystem _files;
    private readonly UbuntuInstallerOptions _options;
    private readonly IRequiredCommandRunner _requiredCommands;
    private readonly DockerPrerequisite _dockerPrerequisite;

    public UbuntuInstallerPlatformAdapter(
        ICommandRunner commands,
        IUbuntuInstallerFileSystem files,
        UbuntuInstallerOptions options,
        IRequiredCommandRunner requiredCommands,
        DockerPrerequisite dockerPrerequisite)
    {
        _commands = commands;
        _files = files;
        _options = options;
        _requiredCommands = requiredCommands;
        _dockerPrerequisite = dockerPrerequisite;
    }

    public string PlatformName => "Ubuntu";

    public bool SupportsInstallActions => true;

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await _dockerPrerequisite.CheckAsync(cancellationToken);

    public async Task<InstallerComponentStatus> GetComponentStatusAsync(
        ProductComponent component,
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => InstallerComponentOperations.StatusFromValidation(
            component,
            await ValidateInstalledStateAsync(session, cancellationToken),
            session.Version);

    public IReadOnlyList<InstallOperation> PlanComponentAction(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session)
        => InstallerComponentOperations.Plan(TargetFor(component), action, session, PlanInstall);

    public IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => InstallerComponentOperations.ExecuteInstallLikeAction(
            TargetFor(component),
            action,
            session,
            ExecuteInstallAsync,
            ExecuteUninstallAsync,
            cancellationToken);

    private async IAsyncEnumerable<InstallProgress> ExecuteUninstallAsync(
        InstallerComponentTarget target,
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = InstallerComponentOperations.Plan(target, InstallerComponentAction.Uninstall, session, PlanInstall);
        var progress = new InstallProgressTracker(operations);

        switch (target)
        {
            case InstallerComponentTarget.Server:
                await _commands.RunAsync("systemctl", $"disable --now {_options.Manifest.ServiceUnitName}", cancellationToken);
                _files.DeleteFile(_options.Paths.ServicePath);
                await _commands.RunAsync("systemctl", "daemon-reload", cancellationToken);
                _files.DeleteDirectory(_options.Paths.ServerDirectory);
                break;
            case InstallerComponentTarget.Cli:
                _files.DeleteFile(_options.Paths.CliSymlinkPath);
                _files.DeleteDirectory(_options.Paths.CliDirectory);
                break;
            case InstallerComponentTarget.Desktop:
                _files.DeleteFile(_options.Paths.DesktopEntryPath);
                await _commands.RunAsync("update-desktop-database", "/usr/share/applications", cancellationToken);
                _files.DeleteFile(_options.Paths.IconPath);
                _files.DeleteDirectory(_options.Paths.DesktopDirectory);
                break;
        }

        yield return progress.Complete(InstallerComponentOperations.TargetOperationKind(target));
        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
    {
        var summary = session.Summary();
        var manifest = _options.Manifest;
        var paths = _options.Paths;
        var operations = new List<InstallOperation>
        {
            new(InstallOperationKind.ValidatePrerequisites, "Validate Docker and Ubuntu prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Use {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, $"Install selected {manifest.DesktopApplicationName} files under {paths.RootDirectory}", true)
        };

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterService, $"Register and start {manifest.ServiceUnitName}", true));
        if (summary.Includes(InstallerComponent.Cli))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterCli, $"Register {paths.CliSymlinkPath}", true));
        if (summary.Includes(InstallerComponent.Desktop))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterDesktop, $"Register {manifest.DesktopApplicationName} desktop launcher", true));

        operations.Add(new InstallOperation(InstallOperationKind.RegisterUninstall, "Register Ubuntu package-manager uninstall metadata", true));
        operations.Add(new InstallOperation(InstallOperationKind.ValidateInstallation, "Validate Ubuntu installed state", false));
        return operations;
    }

    public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = PlanInstall(session);
        var progress = new InstallProgressTracker(operations);

        _files.CreateDirectory(_options.Paths.DataDirectory);
        _files.WriteText(_options.Paths.LogPath, "");
        _files.WriteText(_options.Paths.ErrorLogPath, "");
        yield return progress.Complete(InstallOperationKind.ValidatePrerequisites);

        yield return progress.Complete(InstallOperationKind.StagePayload);

        var summary = session.Summary();
        _files.ResetDirectory(_options.Paths.RootDirectory);
        if (summary.Includes(InstallerComponent.Desktop))
        {
            _files.CopyDirectory(_options.Payload.DesktopDirectory, _options.Paths.DesktopDirectory);
            _files.CopyFile(_options.Payload.IconPath, _options.Paths.IconPath);
            _files.SetExecutable(_options.Paths.DesktopExecutable);
        }

        if (summary.Includes(InstallerComponent.Server))
        {
            _files.CopyDirectory(_options.Payload.ServerDirectory, _options.Paths.ServerDirectory);
            _files.CopyFile(_options.Payload.ServiceFilePath, _options.Paths.ServicePath);
            _files.SetExecutable(_options.Paths.ServerExecutable);
        }

        if (summary.Includes(InstallerComponent.Cli))
        {
            _files.CopyDirectory(_options.Payload.CliDirectory, _options.Paths.CliDirectory);
            _files.SetExecutable(_options.Paths.CliExecutable);
        }
        yield return progress.Complete(InstallOperationKind.InstallFiles);

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
        {
            await _requiredCommands.RunAsync("systemctl", "daemon-reload", cancellationToken);
            await _requiredCommands.RunAsync("systemctl", $"enable --now {_options.Manifest.ServiceUnitName}", cancellationToken);
            yield return progress.Complete(InstallOperationKind.RegisterService);
        }

        if (summary.Includes(InstallerComponent.Cli))
        {
            _files.CreateSymbolicLink(_options.Paths.CliSymlinkPath, _options.Paths.CliExecutable);
            yield return progress.Complete(InstallOperationKind.RegisterCli);
        }

        if (summary.Includes(InstallerComponent.Desktop))
        {
            _files.WriteText(_options.Paths.DesktopEntryPath, _options.Manifest.DesktopEntryText(_options.Paths.DesktopExecutable, session.Version.ToString()));
            await _commands.RunAsync("update-desktop-database", "/usr/share/applications", cancellationToken);
            yield return progress.Complete(InstallOperationKind.RegisterDesktop);
        }

        await _commands.RunAsync("dpkg-query", $"-W {_options.Manifest.PackageName}", cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterUninstall);

        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    private static InstallerComponentTarget TargetFor(ProductComponent component)
        => Enum.TryParse<InstallerComponentTarget>(component.Id, true, out var t)
            ? t
            : throw new NotSupportedException($"Component '{component.Id}' is not supported by the Ubuntu adapter.");

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        var service = await _commands.RunAsync("systemctl", $"is-enabled {_options.Manifest.ServiceUnitName}", cancellationToken);
        var running = await _commands.RunAsync("systemctl", $"is-active {_options.Manifest.ServiceUnitName}", cancellationToken);
        var cli = await _commands.RunAsync("bash", $"-lc \"command -v {_options.Manifest.CliCommandName}\"", cancellationToken);

        return PostInstallValidation.Validate(new InstalledState(
            ServiceRegistered: service.ExitCode == 0,
            ServiceRunning: running.ExitCode == 0,
            CliAvailableFromFreshShell: cli.ExitCode == 0,
            DesktopInstalled: _files.FileExists(_options.Paths.DesktopEntryPath),
            InstallerVersion: session.Version,
            CliVersion: session.Version,
            ServerVersion: session.Version,
            DesktopVersion: session.Version), session.Version);
    }

}
