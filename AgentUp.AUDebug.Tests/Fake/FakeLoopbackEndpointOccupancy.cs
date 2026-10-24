using AgentUp.AUDebug.Features.Validation.Interfaces;

namespace AgentUp.AUDebug.Tests.Fake;

public sealed class FakeLoopbackEndpointOccupancy : ILoopbackEndpointOccupancy
{
    public bool Occupied { get; set; }

    public string? LastUrl { get; private set; }

    public Task<bool> IsOccupiedAsync(string url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastUrl = url;
        return Task.FromResult(Occupied);
    }
}
