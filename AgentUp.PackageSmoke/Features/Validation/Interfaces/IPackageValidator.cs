using AgentUp.PackageSmoke.Features.Validation.DTOs;

namespace AgentUp.PackageSmoke.Features.Validation.Services;

public interface IPackageValidator
{
    Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default);
}
