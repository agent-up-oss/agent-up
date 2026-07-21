using AgentUp.CLI.Features.Workspaces.Interfaces;
using AgentUp.CLI.Features.Workspaces.Models;

namespace AgentUp.CLI.Features.Workspaces.Providers;

public sealed class WorkspaceIdentityProvider : IWorkspaceIdentityProvider
{
    public async Task<WorkspaceIdentity> ReadAsync(string workingDirectory)
    {
        var git = new GitReader(workingDirectory);
        try
        {
            return new WorkspaceIdentity(
                RepositoryPath: await git.GetRepoRootAsync(),
                Branch: await git.GetBranchAsync(),
                Commit: await git.GetCommitAsync());
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new WorkspaceIdentity(
                RepositoryPath: workingDirectory,
                Branch: "not on a git branch",
                Commit: "");
        }
    }
}
