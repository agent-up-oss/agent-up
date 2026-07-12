namespace AgentUp.Server.Features.Ports.Services;

public interface IPortAllocationService
{
    Task<int> GetBasePortAsync(string workspaceId);
    Task ReleaseAsync(string workspaceId);
}
