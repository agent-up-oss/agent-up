using AgentUp.Server.Features.Files.Interfaces;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.DTOs;

namespace AgentUp.Server.Features.Files.Providers;

public sealed class WorkspaceGitChangeProvider(GitQueryController git) : IWorkspaceFileChangeProvider
{
    public async Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(string workspaceId, CancellationToken cancellationToken)
        => await git.GetWorktreeChangesAsync(workspaceId, cancellationToken) ?? [];
}
