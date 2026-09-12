namespace AgentUp.AUDebug.Features.Desktop.Interfaces;

public interface IDesktopWorkspaceClient
{
    Task StartByNameAsync(string workspaceName, string password, CancellationToken cancellationToken);
}
