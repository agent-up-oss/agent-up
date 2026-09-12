namespace AgentUp.AUDebug.Features.Desktop.Interfaces;

public interface IDesktopWindowDriver
{
    Task WaitForWindowAsync(CancellationToken cancellationToken);
    Task<bool> HasWindowAsync(CancellationToken cancellationToken);
    Task CaptureAsync(string outputPath, CancellationToken cancellationToken);
    Task LoginAsync(string password, CancellationToken cancellationToken);
}
