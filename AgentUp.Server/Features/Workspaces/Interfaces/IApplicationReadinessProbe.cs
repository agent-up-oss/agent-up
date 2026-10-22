namespace AgentUp.Server.Features.Workspaces.Interfaces;

public interface IApplicationReadinessProbe
{
    Task<bool> TcpReadyAsync(int port, CancellationToken cancellationToken);

    Task<bool> HttpReadyAsync(int port, string path, CancellationToken cancellationToken);
}
