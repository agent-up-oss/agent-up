using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
using AgentUp.Installers.Features.Installation.Services;

namespace AgentUp.Installers.Features.Installation.Providers;

public sealed class FakeInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    private readonly Dictionary<InstallerComponentTarget, InstallerComponentStatus> _statuses = [];

    public string PlatformName { get; }

    public bool SupportsInstallActions => true;

    public FakeInstallerPlatformAdapter(string platformName = "Dry run")
    {
        PlatformName = platformName;
        foreach (var target in Enum.GetValues<InstallerComponentTarget>())
            _statuses[target] = new InstallerComponentStatus(target, InstallerComponentStatusKind.NotInstalled);
    }

    public Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new DockerStatus(
            DockerStatusKind.Operational,
            "Docker is operational",
            "Dry-run adapter reports Docker as operational.",
            new Version(27, 0, 0)));

    public Task<InstallerComponentStatus> GetComponentStatusAsync(
        InstallerComponentTarget target,
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_statuses[target]);

    public IReadOnlyList<InstallOperation> PlanComponentAction(
        InstallerComponentTarget target,
        InstallerComponentAction action,
        InstallerSession session)
        =>
        [
            new(InstallOperationKind.ValidatePrerequisites, $"Validate {DisplayName(target)} prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Stage {DisplayName(target)} payload", false),
            new(ComponentOperationKind(target), $"{ActionName(action)} {DisplayName(target)}", true),
            new(InstallOperationKind.ValidateInstallation, $"Validate {DisplayName(target)} state", false)
        ];

    public async IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
        InstallerComponentTarget target,
        InstallerComponentAction action,
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = PlanComponentAction(target, action, session);
        var progress = new InstallProgressTracker(operations);
        _statuses[target] = new InstallerComponentStatus(
            target,
            action == InstallerComponentAction.Uninstall ? InstallerComponentStatusKind.Uninstalling : InstallerComponentStatusKind.Installing,
            Message: $"{ActionName(action)} in progress");

        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return progress.Complete(operation.Kind);
        }

        _statuses[target] = action == InstallerComponentAction.Uninstall
            ? new InstallerComponentStatus(target, InstallerComponentStatusKind.NotInstalled)
            : new InstallerComponentStatus(target, InstallerComponentStatusKind.Installed, session.Version, session.Version);
    }

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        =>
        [
            new(InstallOperationKind.ValidatePrerequisites, "Validate prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Stage {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, "Install selected components", true),
            new(InstallOperationKind.RegisterService, $"Register {session.Manifest.ServiceName} service", true),
            new(InstallOperationKind.RegisterCli, $"Register {session.Manifest.CliCommandName} CLI", true),
            new(InstallOperationKind.RegisterDesktop, "Register desktop application", true),
            new(InstallOperationKind.RegisterUninstall, "Register native uninstall entry", true),
            new(InstallOperationKind.ValidateInstallation, "Validate installed state", false)
        ];

    public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operations = PlanInstall(session);
        var progress = new InstallProgressTracker(operations);
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return progress.Complete(operation.Kind);
        }
    }

    public Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PostInstallValidation.Validate(
            new InstalledState(
                ServiceRegistered: true,
                ServiceRunning: true,
                CliAvailableFromFreshShell: true,
                DesktopInstalled: true,
                InstallerVersion: session.Version,
                CliVersion: session.Version,
                ServerVersion: session.Version,
                DesktopVersion: session.Version),
            session.Version));

    private static InstallOperationKind ComponentOperationKind(InstallerComponentTarget target) =>
        target switch
        {
            InstallerComponentTarget.Server => InstallOperationKind.RegisterService,
            InstallerComponentTarget.Cli => InstallOperationKind.RegisterCli,
            InstallerComponentTarget.Desktop => InstallOperationKind.RegisterDesktop,
            _ => InstallOperationKind.InstallFiles
        };

    private static string DisplayName(InstallerComponentTarget target) =>
        target switch
        {
            InstallerComponentTarget.Cli => "CLI",
            _ => target.ToString()
        };

    private static string ActionName(InstallerComponentAction action) =>
        action switch
        {
            InstallerComponentAction.Install => "Install",
            InstallerComponentAction.Update => "Update",
            InstallerComponentAction.Uninstall => "Uninstall",
            InstallerComponentAction.Repair => "Repair",
            _ => action.ToString()
        };
}
