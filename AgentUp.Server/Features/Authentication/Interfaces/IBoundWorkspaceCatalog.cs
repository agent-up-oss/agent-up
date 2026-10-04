namespace AgentUp.Server.Features.Authentication.Interfaces;

public interface IBoundWorkspaceCatalog
{
    Task<bool> PathTargetsWorkspaceAsync(
        string boundWorkspace,
        string path,
        CancellationToken cancellationToken);
}
