using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Services;

namespace AgentUp.Desktop.Features.Workspaces.Controllers;

public sealed class WorkspacesController
{
    private readonly WorkspaceListService _service;

    public WorkspacesController(WorkspaceListService service)
    {
        _service = service;
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
        => await _service.ListAsync(cancellationToken);

    public async Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _service.GetByIdAsync(workspaceId, cancellationToken);

    public async Task<WorkspaceDto> CloneAsync(
        string repository,
        string revision,
        bool derived = false,
        CancellationToken cancellationToken = default)
        => await _service.CloneAsync(
            new CloneSourceRequestDto(repository, revision)
            {
                Derived = derived,
                Revision = derived ? revision : null
            },
            cancellationToken);

    public Task<WorkspaceDto> RefreshCloneAsync(
        string workspaceId,
        string? revision = null,
        CancellationToken cancellationToken = default)
        => _service.RefreshCloneAsync(workspaceId, revision, cancellationToken);

    public async Task StartAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _service.StartAsync(workspaceId, cancellationToken);

    public async Task StopAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _service.StopAsync(workspaceId, cancellationToken);

    public async Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _service.DeleteAsync(workspaceId, cancellationToken);

    public async Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
        => await _service.GetOverviewAsync(workspaceId, cancellationToken);
}
