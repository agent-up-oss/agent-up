using AgentUp.Installers.Features.Flow;
using AgentUp.Installers.Features.Prerequisites;
using AgentUp.Installers.Features.Validation;

namespace AgentUp.Installers.Features.Execution;

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
        for (var index = 0; index < operations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operation = operations[index];
            await Task.Yield();
            yield return new InstallProgress(operation.Kind, operation.Title, index + 1, operations.Count);
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
