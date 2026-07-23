using AgentUp.Server.Features.Ports.Services;

namespace AgentUp.Server.Features.Ports.Controllers;

public sealed class PortsController
{
    private readonly IPortAllocationService _service;

    public PortsController(IPortAllocationService service)
    {
        _service = service;
    }

    public async Task<int> GetBasePortAsync(string workspaceId)
        => await _service.GetBasePortAsync(workspaceId);

    public async Task<int> GetConflictFreeBasePortAsync(string workspaceId, int portCount)
        => await _service.GetConflictFreeBasePortAsync(workspaceId, portCount);

    public async Task ReleaseAsync(string workspaceId)
        => await _service.ReleaseAsync(workspaceId);
}
