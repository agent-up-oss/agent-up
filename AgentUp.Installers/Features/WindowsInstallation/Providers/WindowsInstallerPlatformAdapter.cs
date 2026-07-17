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
    private readonly RequiredCommandRunner _requiredCommands;

    public WindowsInstallerPlatformAdapter(
        ICommandRunner commands,
        IWindowsInstallerFileSystem files,
        WindowsInstallerOptions options)
    {
        _commands = commands;
        _files = files;
        _options = options;
        _requiredCommands = new RequiredCommandRunner(commands);
    }

    public string PlatformName => "Windows";

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await new DockerPrerequisite(new DockerPrerequisiteProvider(_commands), new Version(27, 0, 0)).CheckAsync(cancellationToken);

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        =>
        [
            new(InstallOperationKind.ValidatePrerequisites, "Validate Docker and Windows prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Use {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, "Install Agent-Up application, server, and CLI files", true),
            new(InstallOperationKind.RegisterService, "Register and start agent-up-server Windows Service", true),
            new(InstallOperationKind.RegisterCli, "Register Agent-Up CLI on machine PATH", true),
            new(InstallOperationKind.RegisterDesktop, "Register Agent-Up Start Menu shortcut", true),
            new(InstallOperationKind.RegisterUninstall, "Register native uninstall handoff", true),
            new(InstallOperationKind.ValidateInstallation, "Validate Windows installed state", false)
        ];

    public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = PlanInstall(session);
        var progress = new InstallProgressTracker(operations);
        var manifest = WindowsInstallerManifest.Create(session.Version.ToString());

        await _commands.RunAsync("sc.exe", $"stop {manifest.ServiceName}", cancellationToken);
        await _commands.RunAsync("sc.exe", $"delete {manifest.ServiceName}", cancellationToken);
        yield return progress.Complete(InstallOperationKind.ValidatePrerequisites);
        yield return progress.Complete(InstallOperationKind.StagePayload);

        _files.ResetDirectory(_options.Paths.DesktopDirectory);
        _files.CopyDirectory(_options.Payload.DesktopDirectory, _options.Paths.DesktopDirectory);
        _files.ResetDirectory(_options.Paths.ServerDirectory);
        _files.CopyDirectory(_options.Payload.ServerDirectory, _options.Paths.ServerDirectory);
        _files.ResetDirectory(_options.Paths.CliDirectory);
        _files.CopyDirectory(_options.Payload.CliDirectory, _options.Paths.CliDirectory);
        _files.CreateDirectory(_options.Paths.BinDirectory);
        _files.WriteText(_options.Paths.CliShimPath, WindowsWixSourceGenerator.CliShimText());
        yield return progress.Complete(InstallOperationKind.InstallFiles);

        await _requiredCommands.RunAsync("sc.exe", WindowsInstallerCommands.ServiceCreateArguments(manifest, _options.Paths), cancellationToken);
        await _requiredCommands.RunAsync("sc.exe", WindowsInstallerCommands.ServiceFailureArguments(manifest), cancellationToken);
        await _requiredCommands.RunAsync("sc.exe", $"start {manifest.ServiceName}", cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterService);

        await _requiredCommands.RunPowerShellAsync(WindowsInstallerCommands.PathUpdatePowerShell(_options.Paths.BinDirectory), cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterCli);

        await _requiredCommands.RunPowerShellAsync(WindowsInstallerCommands.ShortcutPowerShell(_options.Paths), cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterDesktop);

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
