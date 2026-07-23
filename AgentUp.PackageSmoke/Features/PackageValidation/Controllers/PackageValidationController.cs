using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Controllers;

public sealed class PackageValidationController
{
    private readonly Func<string, IPackageValidator> _validators;

    public PackageValidationController(Func<string, IPackageValidator> validators)
    {
        _validators = validators;
    }

    public async Task<PackageValidationResult> ValidateAsync(
        PackageValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validator = _validators(request.Platform);
        return await validator.ValidateAsync(request, cancellationToken);
    }
}
