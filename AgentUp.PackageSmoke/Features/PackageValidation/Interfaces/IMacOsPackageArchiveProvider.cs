using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

public interface IMacOsPackageArchiveProvider
{
    Task<PackageArchiveOperationResult> ExpandAsync(string archive, string expandedDirectory, CancellationToken cancellationToken = default);
    string FindFirst(string root, string suffix);
    string FindDistribution(string root);
}
