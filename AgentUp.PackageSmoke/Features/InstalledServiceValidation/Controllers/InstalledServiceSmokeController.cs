using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Interfaces;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.Controllers;

public sealed class InstalledServiceSmokeController
{
    private readonly Func<string, IInstalledServiceSmokeValidator> _validators;

    public InstalledServiceSmokeController(Func<string, IInstalledServiceSmokeValidator> validators)
    {
        _validators = validators;
    }

    public async Task<InstalledServiceSmokeResult> ValidateAsync(
        InstalledServiceSmokeRequest request,
        CancellationToken cancellationToken = default)
    {
        using var validator = _validators(request.Platform);
        return await validator.ValidateAsync(request, cancellationToken);
    }
}
