using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

public interface IWindowsPackageArchiveProvider
{
    Task<PackageArchiveOperationResult> CreateLayoutAsync(string installer, string layoutDirectory, CancellationToken cancellationToken = default);
}
