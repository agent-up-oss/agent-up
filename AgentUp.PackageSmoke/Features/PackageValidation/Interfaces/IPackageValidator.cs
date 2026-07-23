using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

public interface IPackageValidator
{
    Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default);
}
