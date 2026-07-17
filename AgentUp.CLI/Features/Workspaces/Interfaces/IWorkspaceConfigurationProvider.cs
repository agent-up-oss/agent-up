using AgentUp.CLI.Features.Workspaces.Models;

namespace AgentUp.CLI.Features.Workspaces.Interfaces;

public interface IWorkspaceConfigurationProvider
{
    Task<WorkspaceConfigurationResult> LoadAsync(string workingDirectory);
}
