using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using AgentUp.Desktop.Features.Workspaces.Services;

namespace AgentUp.Desktop.Features.Workspaces.Controllers;

public sealed class WorkspacesController : IWorkspaceFirstRunBoundary
{
    private readonly WorkspaceListService _service;

    public WorkspacesController(WorkspaceListService service)
    {
        _service = service;
    }

    public async Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
        => await _service.ListAsync(cancellationToken);

    public async Task CleanupTutorialWorkspacesAsync(CancellationToken cancellationToken = default)
        => await _service.CleanupTutorialWorkspacesAsync(cancellationToken);
}
