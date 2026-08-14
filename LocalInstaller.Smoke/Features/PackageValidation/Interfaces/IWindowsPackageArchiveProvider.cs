using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.PackageValidation.Interfaces;

public interface IWindowsPackageArchiveProvider
{
    Task<PackageArchiveOperationResult> CreateLayoutAsync(string installer, string layoutDirectory, CancellationToken cancellationToken = default);
}
