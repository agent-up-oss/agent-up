namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;

public interface IServerProbe
{
    Task<string?> WaitForReadyAsync(
        string primaryUrl,
        string fallbackUrl,
        string outputFile,
        CancellationToken cancellationToken = default);
}
