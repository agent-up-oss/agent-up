using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
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
    private readonly RequiredCommandRunner _requiredCommands;

    public UbuntuInstallerPlatformAdapter(
        ICommandRunner commands,
        IUbuntuInstallerFileSystem files,
        UbuntuInstallerOptions options)
    {
        _commands = commands;
        _files = files;
        _options = options;
        _requiredCommands = new RequiredCommandRunner(commands);
    }

    public string PlatformName => "Ubuntu";

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await new DockerPrerequisite(_commands, new Version(27, 0, 0)).CheckAsync(cancellationToken);

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        =>
        [
            new(InstallOperationKind.ValidatePrerequisites, "Validate Docker and Ubuntu prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Use {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, "Install Agent-Up files under /opt/agent-up", true),
            new(InstallOperationKind.RegisterService, "Register and start agent-up-server.service", true),
            new(InstallOperationKind.RegisterCli, "Register /usr/bin/agent-up", true),
            new(InstallOperationKind.RegisterDesktop, "Register Agent-Up desktop launcher", true),
            new(InstallOperationKind.RegisterUninstall, "Register Ubuntu package-manager uninstall metadata", true),
            new(InstallOperationKind.ValidateInstallation, "Validate Ubuntu installed state", false)
        ];

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

        _files.ResetDirectory(_options.Paths.RootDirectory);
        _files.CopyDirectory(_options.Payload.DesktopDirectory, _options.Paths.DesktopDirectory);
        _files.CopyDirectory(_options.Payload.ServerDirectory, _options.Paths.ServerDirectory);
        _files.CopyDirectory(_options.Payload.CliDirectory, _options.Paths.CliDirectory);
        _files.CopyFile(_options.Payload.ServiceFilePath, _options.Paths.ServicePath);
        _files.CopyFile(_options.Payload.IconPath, _options.Paths.IconPath);
        _files.SetExecutable(_options.Paths.DesktopExecutable);
        _files.SetExecutable(_options.Paths.ServerExecutable);
        _files.SetExecutable(_options.Paths.CliExecutable);
        yield return progress.Complete(InstallOperationKind.InstallFiles);

        await _requiredCommands.RunAsync("systemctl", "daemon-reload", cancellationToken);
        await _requiredCommands.RunAsync("systemctl", "enable --now agent-up-server.service", cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterService);

        _files.CreateSymbolicLink(_options.Paths.CliSymlinkPath, _options.Paths.CliExecutable);
        yield return progress.Complete(InstallOperationKind.RegisterCli);

        _files.WriteText(_options.Paths.DesktopEntryPath, UbuntuInstallerManifest.DesktopEntryText(session.Version));
        await _commands.RunAsync("update-desktop-database", "/usr/share/applications", cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterDesktop);

        await _commands.RunAsync("dpkg-query", "-W agent-up", cancellationToken);
        yield return progress.Complete(InstallOperationKind.RegisterUninstall);

        yield return progress.Complete(InstallOperationKind.ValidateInstallation);
    }

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        var service = await _commands.RunAsync("systemctl", "is-enabled agent-up-server.service", cancellationToken);
        var running = await _commands.RunAsync("systemctl", "is-active agent-up-server.service", cancellationToken);
        var cli = await _commands.RunAsync("bash", "-lc \"command -v agent-up\"", cancellationToken);

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
