namespace AgentUp.AUDebug.Features.Mobile.Interfaces;

public interface IMobileSurfaceDriver
{
    Task LoginAsync(string serverUrl, string password, CancellationToken cancellationToken);
}
