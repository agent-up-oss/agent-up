using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;
using AgentUp.Installers.Features.Installation.Services;

namespace AgentUp.Installers.Features.Installation.Providers;

public sealed class FakeInstallerPlatformAdapter : IInstallerPlatformAdapter
{
    public string PlatformName { get; }

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

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        =>
        [
            new(InstallOperationKind.ValidatePrerequisites, "Validate prerequisites", false),
            new(InstallOperationKind.StagePayload, $"Stage {session.Payload.Description}", false),
            new(InstallOperationKind.InstallFiles, "Install selected components", true),
            new(InstallOperationKind.RegisterService, "Register agent-up-server service", true),
            new(InstallOperationKind.RegisterCli, "Register agent-up CLI", true),
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
}
