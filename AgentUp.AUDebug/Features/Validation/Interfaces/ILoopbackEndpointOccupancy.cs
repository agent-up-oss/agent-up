namespace AgentUp.AUDebug.Features.Validation.Interfaces;

public interface ILoopbackEndpointOccupancy
{
    Task<bool> IsOccupiedAsync(string url, CancellationToken cancellationToken);
}
