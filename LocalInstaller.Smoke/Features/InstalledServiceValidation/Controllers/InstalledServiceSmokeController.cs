using LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Interfaces;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.Controllers;

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
