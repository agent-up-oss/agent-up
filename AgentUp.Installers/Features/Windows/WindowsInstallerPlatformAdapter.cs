using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Flow;
using AgentUp.Installers.Features.Prerequisites;
using AgentUp.Installers.Features.Validation;

namespace AgentUp.Installers.Features.Windows;

public sealed class WindowsInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    private readonly ICommandRunner _commands;
    private readonly IWindowsInstallerFileSystem _files;
    private readonly WindowsInstallerOptions _options;

    public WindowsInstallerPlatformAdapter(
        ICommandRunner commands,
        IWindowsInstallerFileSystem files,
        WindowsInstallerOptions options)
    {
        _commands = commands;
        _files = files;
        _options = options;
    }

    public string PlatformName => "Windows";

    public async Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => await new DockerPrerequisite(_commands, new Version(27, 0, 0)).CheckAsync(cancellationToken);

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
        var completed = 0;
        var manifest = WindowsInstallerManifest.Create(session.Version.ToString());

        await _commands.RunAsync("sc.exe", $"stop {manifest.ServiceName}", cancellationToken);
        await _commands.RunAsync("sc.exe", $"delete {manifest.ServiceName}", cancellationToken);
        yield return Progress(operations, ref completed, InstallOperationKind.ValidatePrerequisites);
        yield return Progress(operations, ref completed, InstallOperationKind.StagePayload);

        _files.ResetDirectory(_options.Paths.DesktopDirectory);
        _files.CopyDirectory(_options.Payload.DesktopDirectory, _options.Paths.DesktopDirectory);
        _files.ResetDirectory(_options.Paths.ServerDirectory);
        _files.CopyDirectory(_options.Payload.ServerDirectory, _options.Paths.ServerDirectory);
        _files.ResetDirectory(_options.Paths.CliDirectory);
        _files.CopyDirectory(_options.Payload.CliDirectory, _options.Paths.CliDirectory);
        _files.CreateDirectory(_options.Paths.BinDirectory);
        _files.WriteText(_options.Paths.CliShimPath, WindowsWixSourceGenerator.CliShimText());
        yield return Progress(operations, ref completed, InstallOperationKind.InstallFiles);

        await RunRequiredAsync("sc.exe", WindowsInstallerCommands.ServiceCreateArguments(manifest, _options.Paths), cancellationToken);
        await RunRequiredAsync("sc.exe", WindowsInstallerCommands.ServiceFailureArguments(manifest), cancellationToken);
        await RunRequiredAsync("sc.exe", $"start {manifest.ServiceName}", cancellationToken);
        yield return Progress(operations, ref completed, InstallOperationKind.RegisterService);

        await RunPowerShellRequiredAsync(WindowsInstallerCommands.PathUpdatePowerShell(_options.Paths.BinDirectory), cancellationToken);
        yield return Progress(operations, ref completed, InstallOperationKind.RegisterCli);

        await RunPowerShellRequiredAsync(WindowsInstallerCommands.ShortcutPowerShell(_options.Paths), cancellationToken);
        yield return Progress(operations, ref completed, InstallOperationKind.RegisterDesktop);

        _files.WriteText(_options.Paths.UninstallScriptPath, WindowsInstallerCommands.UninstallScript(manifest, _options.Paths));
        await RunPowerShellRequiredAsync(WindowsInstallerCommands.UninstallRegistryPowerShell(manifest, _options.Paths), cancellationToken);
        yield return Progress(operations, ref completed, InstallOperationKind.RegisterUninstall);
        yield return Progress(operations, ref completed, InstallOperationKind.ValidateInstallation);
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

    private async Task RunPowerShellRequiredAsync(string command, CancellationToken cancellationToken)
        => await RunRequiredAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"", cancellationToken);

    private async Task RunRequiredAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync(fileName, arguments, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} {arguments} failed: {result.Stderr}{result.Stdout}");
    }

    private static InstallProgress Progress(IReadOnlyList<InstallOperation> operations, ref int completed, InstallOperationKind kind)
    {
        var operation = operations.First(item => item.Kind == kind);
        completed++;
        return new InstallProgress(operation.Kind, operation.Title, completed, operations.Count);
    }
}
