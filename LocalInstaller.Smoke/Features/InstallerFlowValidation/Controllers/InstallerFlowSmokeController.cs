using LocalInstaller.Smoke.Features.InstallerFlowValidation.DTOs;
using LocalInstaller.Smoke.Features.InstallerFlowValidation.Services;
using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.InstallerFlowValidation.Controllers;

public sealed class InstallerFlowSmokeController
{
    private readonly InstallerFlowSmokeValidator _validator;

    public InstallerFlowSmokeController(InstallerFlowSmokeValidator validator)
    {
        _validator = validator;
    }

    public async Task<PackageValidationResult> ValidateAsync(
        string platform,
        string workDirectory,
        InstallerFlowProductManifest? product = null,
        CancellationToken cancellationToken = default)
        => await _validator.ValidateAsync(platform, workDirectory, product?.ToManifest(), cancellationToken);
}
