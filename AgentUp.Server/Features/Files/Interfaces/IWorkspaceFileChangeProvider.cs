using AgentUp.Server.Features.Git.DTOs;

namespace AgentUp.Server.Features.Files.Interfaces;

/// <summary>What Git says has changed in a workspace, relative to the workspace folder.</summary>
public interface IWorkspaceFileChangeProvider
{
    Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(string workspaceId, CancellationToken cancellationToken);
}
