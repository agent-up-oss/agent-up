namespace AgentUp.PackageSmoke.Features.InstalledServices;

public interface IInstalledServiceSmokeValidator
{
    Task<InstalledServiceSmokeResult> ValidateAsync(InstalledServiceSmokeRequest request, CancellationToken cancellationToken = default);
}
