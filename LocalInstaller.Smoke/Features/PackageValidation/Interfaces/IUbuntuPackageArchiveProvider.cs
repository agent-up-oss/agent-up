using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.PackageValidation.Interfaces;

public interface IUbuntuPackageArchiveProvider
{
    Task<PackageArchiveOperationResult> ExtractRootAsync(string archive, string rootDirectory, CancellationToken cancellationToken = default);
    Task<PackageArchiveOperationResult> ExtractControlAsync(string archive, string controlDirectory, CancellationToken cancellationToken = default);
}
