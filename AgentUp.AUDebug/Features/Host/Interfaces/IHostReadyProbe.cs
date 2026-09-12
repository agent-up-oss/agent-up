namespace AgentUp.AUDebug.Features.Host.Interfaces;

public interface IHostReadyProbe
{
    Task WaitAsync(string url, CancellationToken cancellationToken);
    Task<bool> CheckAsync(string url, CancellationToken cancellationToken);
}
