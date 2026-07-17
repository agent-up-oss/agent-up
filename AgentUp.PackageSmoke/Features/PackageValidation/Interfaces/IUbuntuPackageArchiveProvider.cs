using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

public interface IUbuntuPackageArchiveProvider
{
    Task<PackageArchiveOperationResult> ExtractRootAsync(string archive, string rootDirectory, CancellationToken cancellationToken = default);
    Task<PackageArchiveOperationResult> ExtractControlAsync(string archive, string controlDirectory, CancellationToken cancellationToken = default);
}
