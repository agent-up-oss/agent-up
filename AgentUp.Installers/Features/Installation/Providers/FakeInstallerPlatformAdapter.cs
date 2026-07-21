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
    private readonly Dictionary<string, InstallerComponentStatus> _statuses = [];

    public string PlatformName { get; }

    public bool SupportsInstallActions => true;

    public FakeInstallerPlatformAdapter(string platformName = "Dry run")
    {
        PlatformName = platformName;
    }

    public Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new DockerStatus(
            DockerStatusKind.Operational,
            "Docker is operational",
            "Dry-run adapter reports Docker as operational.",
            new Version(27, 0, 0)));

    public Task<InstallerComponentStatus> GetComponentStatusAsync(
        ProductComponent component,
        InstallerSession session,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized(session);
        ValidateComponent(component, session);
        return Task.FromResult(_statuses[component.Id]);
    }

    public IReadOnlyList<InstallOperation> PlanComponentAction(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session)
        =>
        [
            new(InstallOperationKind.ValidatePrerequisites, $"Validate {component.DisplayName} prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Stage {component.DisplayName} payload", false),
            new(ComponentOperationKind(component), $"{ActionName(action)} {component.DisplayName}", true),
            new(InstallOperationKind.ValidateInstallation, $"Validate {component.DisplayName} state", false)
        ];

    public async IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureInitialized(session);
        ValidateComponent(component, session);

        var operations = PlanComponentAction(component, action, session);
        var progress = new InstallProgressTracker(operations);
        _statuses[component.Id] = new InstallerComponentStatus(
            component,
            action == InstallerComponentAction.Uninstall ? InstallerComponentStatusKind.Uninstalling : InstallerComponentStatusKind.Installing,
            Message: $"{ActionName(action)} in progress");

        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return progress.Complete(operation.Kind);
        }

        _statuses[component.Id] = action == InstallerComponentAction.Uninstall
            ? new InstallerComponentStatus(component, InstallerComponentStatusKind.NotInstalled)
            : new InstallerComponentStatus(component, InstallerComponentStatusKind.Installed, session.Version, session.Version);
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

    private void EnsureInitialized(InstallerSession session)
    {
        foreach (var component in session.Manifest.Components.Where(component => !_statuses.ContainsKey(component.Id)))
            _statuses[component.Id] = new InstallerComponentStatus(component, InstallerComponentStatusKind.NotInstalled);
    }

    private static void ValidateComponent(ProductComponent component, InstallerSession session)
    {
        if (!session.Manifest.Components.Any(c => c.Id == component.Id))
            throw new InvalidOperationException(
                $"Component '{component.Id}' is not declared in the '{session.Manifest.ProductName}' manifest.");
    }

    private static InstallOperationKind ComponentOperationKind(ProductComponent component) =>
        component.Id switch
        {
            "server" => InstallOperationKind.RegisterService,
            "cli" => InstallOperationKind.RegisterCli,
            "desktop" => InstallOperationKind.RegisterDesktop,
            _ => InstallOperationKind.InstallFiles
        };

    private static string ActionName(InstallerComponentAction action) =>
        action switch
        {
            InstallerComponentAction.Install => "Install",
            InstallerComponentAction.Update => "Update",
            InstallerComponentAction.Uninstall => "Uninstall",
            InstallerComponentAction.Repair => "Repair",
            _ => $"{action}"
        };
}
