using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Repositories;

namespace AgentUp.Server.Tests.Fake;

internal sealed class InMemoryWorkspaceRepository : IWorkspaceRepository
{
    public Task<IReadOnlyList<Workspace>> LoadAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Workspace>>([]);

    public Task SaveAllAsync(IReadOnlyList<Workspace> workspaces, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
