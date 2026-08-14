using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.PackageValidation.Interfaces;

public interface IPackageValidator
{
    Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default);
}
