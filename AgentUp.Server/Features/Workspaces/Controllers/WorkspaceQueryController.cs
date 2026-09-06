using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Workspaces.Controllers;

public sealed class WorkspaceQueryController
{
    private readonly WorkspaceRegistry _registry;

    public WorkspaceQueryController(WorkspaceRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<Workspace> GetAll() => _registry.GetAll();

    public Workspace? GetById(string id) => _registry.GetById(id);

    public bool HasApplication(string id, string applicationName) =>
        _registry.GetById(id)?.Applications.Any(app => string.Equals(app.Name, applicationName, StringComparison.Ordinal)) ?? false;

    public async Task<Workspace> RegisterAsync(RegisterWorkspaceRequest request)
        => await _registry.RegisterAsync(request);

    public async Task ReallocatePortsAsync(string workspaceId)
        => await _registry.ReallocatePortsAsync(workspaceId);
}
