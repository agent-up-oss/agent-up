using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
using AgentUp.Installers.Features.WindowsInstallation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using AgentUp.Installers.Features.WindowsInstallation.Services;

namespace AgentUp.Installers.Features.WindowsInstallation.Providers;

public sealed class WindowsInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    private readonly ICommandRunner _commands;
    private readonly IWindowsInstallerFileSystem _files;
    private readonly WindowsInstallerOptions _options;
    private readonly IRequiredCommandRunner _requiredCommands;
    private readonly DockerPrerequisite _dockerPrerequisite;

    public WindowsInstallerPlatformAdapter(
        ICommandRunner commands,
        IWindowsInstallerFileSystem files,
        WindowsInstallerOptions options,
        IRequiredCommandRunner requiredCommands,
        DockerPrerequisite dockerPrerequisite)
    {
        _commands = commands;
        _files = files;
        _options = options;
        _requiredCommands = requiredCommands;
        _dockerPrerequisite = dockerPrerequisite;
    }

    public string PlatformName => "Windows";

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
    {
        if (action == InstallerComponentAction.Uninstall)
            return ExecuteComponentUninstallAsync(target, session, cancellationToken);

        return InstallerComponentOperations.ExecuteInstallLikeAction(
            target,
            action,
            session,
            ExecuteInstallAsync,
            cancellationToken);
    }

    private async IAsyncEnumerable<InstallProgress> ExecuteComponentUninstallAsync(
        InstallerComponentTarget target,
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = InstallerComponentOperations.Plan(target, InstallerComponentAction.Uninstall, session, _ => []);
        var progress = new InstallProgressTracker(operations);
        var manifest = WindowsInstallerManifest.Create(session.Version.ToString());

        switch (target)
        {
            case InstallerComponentTarget.Server:
                await _requiredCommands.RunPowerShellAsync(WindowsInstallerCommands.PrepareExistingServicePowerShell(manifest), cancellationToken);
                _files.DeleteDirectory(_options.Paths.ServerDirectory);
                break;
            case InstallerComponentTarget.Cli:
                await _requiredCommands.RunPowerShellAsync(WindowsInstallerCommands.PathRemovePowerShell(_options.Paths.BinDirectory), cancellationToken);
                _files.DeleteDirectory(_options.Paths.CliDirectory);
                _files.DeleteDirectory(_options.Paths.BinDirectory);
                break;
            case InstallerComponentTarget.Desktop:
                _files.DeleteFile(_options.Paths.StartMenuShortcutPath);
                _files.DeleteDirectory(_options.Paths.DesktopDirectory);
                break;
        }

        yield return progress.Complete(operations[0].Kind);
        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
    {
        var summary = session.Summary();
        var operations = new List<InstallOperation>
        {
            new(InstallOperationKind.ValidatePrerequisites, "Validate Docker and Windows prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Use {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, "Install selected Agent-Up files", true)
        };

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterService, "Register and start agent-up-server Windows Service", true));
        if (summary.Includes(InstallerComponent.Cli))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterCli, "Register Agent-Up CLI on machine PATH", true));
        if (summary.Includes(InstallerComponent.Desktop))
            operations.Add(new InstallOperation(InstallOperationKind.RegisterDesktop, "Register Agent-Up Start Menu shortcut", true));

        operations.Add(new InstallOperation(InstallOperationKind.RegisterUninstall, "Register native uninstall handoff", true));
        operations.Add(new InstallOperation(InstallOperationKind.ValidateInstallation, "Validate Windows installed state", false));
        return operations;
    }

    public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = PlanInstall(session);
        var progress = new InstallProgressTracker(operations);
        var manifest = WindowsInstallerManifest.Create(session.Version.ToString());
        var summary = session.Summary();

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
            await _requiredCommands.RunPowerShellAsync(WindowsInstallerCommands.PrepareExistingServicePowerShell(manifest), cancellationToken);
        yield return progress.Complete(InstallOperationKind.ValidatePrerequisites);
        yield return progress.Complete(InstallOperationKind.StagePayload);

        if (summary.Includes(InstallerComponent.Desktop))
        {
            _files.ResetDirectory(_options.Paths.DesktopDirectory);
            _files.CopyDirectory(_options.Payload.DesktopDirectory, _options.Paths.DesktopDirectory);
        }

        if (summary.Includes(InstallerComponent.Server))
        {
            _files.ResetDirectory(_options.Paths.ServerDirectory);
            _files.CopyDirectory(_options.Payload.ServerDirectory, _options.Paths.ServerDirectory);
        }

        if (summary.Includes(InstallerComponent.Cli))
        {
            _files.ResetDirectory(_options.Paths.CliDirectory);
            _files.CopyDirectory(_options.Payload.CliDirectory, _options.Paths.CliDirectory);
            _files.CreateDirectory(_options.Paths.BinDirectory);
            _files.WriteText(_options.Paths.CliShimPath, WindowsWixSourceGenerator.CliShimText());
        }
        yield return progress.Complete(InstallOperationKind.InstallFiles);

        if (summary.Includes(InstallerComponent.Server) || summary.Includes(InstallerComponent.NativeService))
        {
            await _requiredCommands.RunAsync("sc.exe", WindowsInstallerCommands.ServiceCreateArguments(manifest, _options.Paths), cancellationToken);
            await _requiredCommands.RunAsync("sc.exe", WindowsInstallerCommands.ServiceFailureArguments(manifest), cancellationToken);
            await _requiredCommands.RunAsync("sc.exe", $"start {manifest.ServiceName}", cancellationToken);
            yield return progress.Complete(InstallOperationKind.RegisterService);
        }

        if (summary.Includes(InstallerComponent.Cli))
        {
            await _requiredCommands.RunPowerShellAsync(WindowsInstallerCommands.PathUpdatePowerShell(_options.Paths.BinDirectory), cancellationToken);
            yield return progress.Complete(InstallOperationKind.RegisterCli);
        }

        if (summary.Includes(InstallerComponent.Desktop))
        {
            await _requiredCommands.RunPowerShellAsync(WindowsInstallerCommands.ShortcutPowerShell(_options.Paths), cancellationToken);
            yield return progress.Complete(InstallOperationKind.RegisterDesktop);
        }

        _files.WriteText(_options.Paths.UninstallScriptPath, WindowsInstallerCommands.UninstallScript(manifest, _options.Paths));
        await _requiredCommands.RunPowerShellAsync(WindowsInstallerCommands.UninstallRegistryPowerShell(manifest, _options.Paths), cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterUninstall);
        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        var manifest = WindowsInstallerManifest.Create(session.Version.ToString());
        var service = await _commands.RunAsync("sc.exe", $"query {manifest.ServiceName}", cancellationToken);
        var cli = await _commands.RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{WindowsInstallerCommands.FreshShellCliLookupPowerShell()}\"", cancellationToken);

        return PostInstallValidation.Validate(new InstalledState(
            ServiceRegistered: service.ExitCode == 0,
            ServiceRunning: service.ExitCode == 0 && service.Stdout.Contains("RUNNING", StringComparison.OrdinalIgnoreCase),
            CliAvailableFromFreshShell: cli.ExitCode == 0,
            DesktopInstalled: _files.FileExists(_options.Paths.DesktopExecutable),
            InstallerVersion: session.Version,
            CliVersion: session.Version,
            ServerVersion: session.Version,
            DesktopVersion: session.Version), session.Version);
    }

}
