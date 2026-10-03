namespace AgentUp.AUDebug.Features.Screens.Interfaces;

public interface IScreenReadyProbe
{
    Task<bool> IsReadyAsync(string url, CancellationToken cancellationToken);

    Task WaitForUrlAsync(string url, CancellationToken cancellationToken);

    Task<bool> HasDesktopWindowAsync(CancellationToken cancellationToken);

    Task WaitForDesktopWindowAsync(CancellationToken cancellationToken);
}
