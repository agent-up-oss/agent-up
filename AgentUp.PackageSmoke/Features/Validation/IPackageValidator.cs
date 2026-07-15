namespace AgentUp.PackageSmoke.Features.Validation;

public interface IPackageValidator
{
    Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default);
}
