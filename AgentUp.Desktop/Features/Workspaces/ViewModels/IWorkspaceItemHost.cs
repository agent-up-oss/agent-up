namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

internal interface IWorkspaceItemHost
{
    Task StartWorkspaceAsync(string workspaceId);

    Task StopWorkspaceAsync(string workspaceId);

    void RequestDeleteWorkspace(string workspaceId, string displayName);
}
