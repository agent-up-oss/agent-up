namespace AgentUp.PackageSmoke.Features.InstalledServices;

public interface IServerProbe
{
    Task<string?> WaitForReadyAsync(
        string primaryUrl,
        string fallbackUrl,
        string outputFile,
        CancellationToken cancellationToken = default);
}
