namespace AgentUp.AUDebug.Features.Mobile.Interfaces;

public interface IMobileSurfaceDriver
{
    string UserDataDirectory { get; }
    Task LoginAsync(string serverUrl, string password, CancellationToken cancellationToken);
}
