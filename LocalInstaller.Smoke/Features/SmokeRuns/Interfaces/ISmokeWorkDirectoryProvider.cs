using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.SmokeRuns.Interfaces;

public interface ISmokeWorkDirectoryProvider
{
    void Prepare(string workDirectory);
    Task WritePackageEnvironmentAsync(string workDirectory, PackageValidationResult result, CancellationToken cancellationToken = default);
}
