namespace AgentUp.Installers.Features.Installation.Models;

public enum InstallOperationKind
{
    ValidatePrerequisites,
    StagePayload,
    InstallFiles,
    RegisterService,
    RegisterCli,
    RegisterDesktop,
    RegisterUninstall,
    ValidateInstallation,
    Rollback
}

public sealed record InstallOperation(
    InstallOperationKind Kind,
    string Title,
    bool RequiresElevation);

public sealed record InstallProgress(
    InstallOperationKind Kind,
    string Message,
    int CompletedOperations,
    int TotalOperations);
