using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;

namespace AgentUp.Desktop.Features.Workspaces.Services;

public sealed class WorkspaceListService
{
    private readonly WorkspaceApiClient _client;

    public WorkspaceListService(WorkspaceApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
        => await _client.ListAsync(cancellationToken);
}
