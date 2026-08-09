using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.PackageValidation.Interfaces;

public interface IMacOsPackageArchiveProvider
{
    Task<PackageArchiveOperationResult> ExpandAsync(string archive, string expandedDirectory, CancellationToken cancellationToken = default);
    string FindFirst(string root, string suffix);
    string FindDistribution(string root);
}
