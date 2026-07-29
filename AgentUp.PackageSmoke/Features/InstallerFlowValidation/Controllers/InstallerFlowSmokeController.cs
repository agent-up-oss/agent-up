using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation.Services;

namespace AgentUp.PackageSmoke.Features.InstallerFlowValidation.Controllers;

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
        ProductManifest? product = null,
        CancellationToken cancellationToken = default)
        => await _validator.ValidateAsync(platform, workDirectory, product, cancellationToken);
}
