using AgentUp.CLI.Features.Workspaces.Models;

namespace AgentUp.CLI.Features.Workspaces.Interfaces;

public interface IWorkspaceIdentityProvider
{
    Task<WorkspaceIdentity> ReadAsync(string workingDirectory);
}
