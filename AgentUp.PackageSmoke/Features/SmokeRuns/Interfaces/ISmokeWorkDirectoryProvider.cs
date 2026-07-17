using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

public interface ISmokeWorkDirectoryProvider
{
    void Prepare(string workDirectory);
    Task WritePackageEnvironmentAsync(string workDirectory, PackageValidationResult result, CancellationToken cancellationToken = default);
}
