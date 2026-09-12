using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;

namespace AgentUp.Desktop.Features.Workspaces.Services;

public sealed class WorkspaceListService
{
    private readonly IWorkspaceApiProvider _client;

    public WorkspaceListService(IWorkspaceApiProvider client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
        => await _client.ListAsync(cancellationToken);

    public async Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _client.GetByIdAsync(workspaceId, cancellationToken);

    public async Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default)
        => await _client.CloneAsync(request, cancellationToken);

    public async Task StartAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _client.StartAsync(workspaceId, cancellationToken);

    public async Task StopAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _client.StopAsync(workspaceId, cancellationToken);

    public async Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _client.DeleteAsync(workspaceId, cancellationToken);

    public async Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
        => await _client.CleanupTutorialWorkspacesAsync(cancellationToken);

    public async Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _client.GetOverviewAsync(workspaceId, cancellationToken);
}
