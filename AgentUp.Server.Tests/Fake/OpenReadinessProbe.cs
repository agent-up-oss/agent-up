using AgentUp.Server.Features.Workspaces.Interfaces;

namespace AgentUp.Server.Tests.Fake;

/// <summary>Answers every readiness probe at once, as an application that is already listening would.</summary>
internal sealed class OpenReadinessProbe : IApplicationReadinessProbe
{
    public Task<bool> TcpReadyAsync(int port, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<bool> HttpReadyAsync(int port, string path, CancellationToken cancellationToken) => Task.FromResult(true);
}
