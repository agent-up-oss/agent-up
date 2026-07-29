using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Controllers;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;
using AgentUp.PackageSmoke.Features.InstallerFlowValidation.Controllers;
using AgentUp.PackageSmoke.Features.PackageValidation.Controllers;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;
using AgentUp.PackageSmoke.Features.SmokeRuns.Interfaces;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.Providers;

public sealed class SmokeValidationProvider : ISmokeValidationProvider
{
    private readonly PackageValidationController _packageValidation;
    private readonly InstallerFlowSmokeController _installerFlow;
    private readonly InstalledServiceSmokeController _installedService;
    private readonly ISmokeWorkDirectoryProvider _workDirectory;

    public SmokeValidationProvider(
        PackageValidationController packageValidation,
        InstallerFlowSmokeController installerFlow,
        InstalledServiceSmokeController installedService,
        ISmokeWorkDirectoryProvider workDirectory)
    {
        _packageValidation = packageValidation;
        _installerFlow = installerFlow;
        _installedService = installedService;
        _workDirectory = workDirectory;
    }

    public async Task<SmokeCommandResult> ValidatePackageAsync(
        SmokeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationRequest = new PackageValidationRequest(
            request.Platform,
            request.RuntimeId,
            request.ArtifactDirectory,
            request.WorkDirectory,
            request.ProductConfig);
        var result = await _packageValidation.ValidateAsync(validationRequest, cancellationToken);
        await _workDirectory.WritePackageEnvironmentAsync(validationRequest.WorkDirectory, result, cancellationToken);
        return new SmokeCommandResult(result.Succeeded, result.Findings);
    }

    public async Task<SmokeCommandResult> ValidateInstallerFlowAsync(
        SmokeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PayloadRoot is not null)
            Environment.SetEnvironmentVariable(InstallerPlatformAdapterFactory.PayloadRootVariable, request.PayloadRoot);

        var result = await _installerFlow.ValidateAsync(
            request.Platform,
            request.WorkDirectory,
            request.ProductConfig is null ? null : ToProductManifest(request.ProductConfig),
            cancellationToken);
        return new SmokeCommandResult(result.Succeeded, result.Findings);
    }

    public async Task<SmokeCommandResult> ValidateInstalledServiceAsync(
        SmokeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var smokeRequest = new InstalledServiceSmokeRequest(
            request.Platform,
            request.RuntimeId,
            request.ArtifactDirectory,
            request.WorkDirectory,
            ProductConfig: request.ProductConfig);
        var result = await _installedService.ValidateAsync(smokeRequest, cancellationToken);
        return new SmokeCommandResult(result.Succeeded, result.Findings);
    }

    private static ProductManifest ToProductManifest(SmokeProductConfig product)
        => new(product.DisplayName, product.ArtifactBaseName, EnvironmentPrefix(product))
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
        };

    private static string EnvironmentPrefix(SmokeProductConfig product)
        => product.ArtifactBaseName.Replace('-', '_').ToUpperInvariant();
}
