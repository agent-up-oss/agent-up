using LocalInstaller.Smoke.Features.InstalledServiceValidation.Controllers;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;
using LocalInstaller.Smoke.Features.InstallerFlowValidation.Controllers;
using LocalInstaller.Smoke.Features.InstallerFlowValidation.DTOs;
using LocalInstaller.Smoke.Features.PackageValidation.Controllers;
using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.SmokeRuns.DTOs;
using LocalInstaller.Smoke.Features.SmokeRuns.Interfaces;

namespace LocalInstaller.Smoke.Features.SmokeRuns.Providers;

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
            request.ProductManifest is null ? null : ToPackageProduct(request.ProductManifest));
        var result = await _packageValidation.ValidateAsync(validationRequest, cancellationToken);
        await _workDirectory.WritePackageEnvironmentAsync(validationRequest.WorkDirectory, result, cancellationToken);
        return new SmokeCommandResult(result.Succeeded, result.Findings);
    }

    public async Task<SmokeCommandResult> ValidateInstallerFlowAsync(
        SmokeCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = ToInstallerFlowProduct(request.Product);
        if (request.PayloadRoot is not null)
            Environment.SetEnvironmentVariable($"{request.Product.ArtifactBaseName.Replace("-", "", StringComparison.Ordinal).ToUpperInvariant()}_INSTALLER_PAYLOAD_ROOT", request.PayloadRoot);

        var result = await _installerFlow.ValidateAsync(
            request.Platform,
            request.WorkDirectory,
            product,
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
            ProductConfig: request.ProductManifest is null ? null : ToInstalledServiceProduct(request.ProductManifest));
        var result = await _installedService.ValidateAsync(smokeRequest, cancellationToken);
        return new SmokeCommandResult(result.Succeeded, result.Findings);
    }

    private static PackageProductManifest ToPackageProduct(SmokeProductManifest product)
        => new(
            product.ServiceName,
            product.CliShimName,
            product.ArtifactBaseName,
            product.DisplayName,
            product.InstallDirName,
            product.WorkspaceConfigFileName,
            product.ServerUrlEnvironmentVariable,
            product.InstallerExecutableName,
            product.DesktopExecutableName,
            product.ServerExecutableName,
            product.CliExecutableName,
            product.TrayExecutableName);

    private static InstalledServiceProductManifest ToInstalledServiceProduct(SmokeProductManifest product)
        => new(
            product.ServiceName,
            product.CliShimName,
            product.ArtifactBaseName,
            product.DisplayName,
            product.InstallDirName,
            product.WorkspaceConfigFileName,
            product.ServerUrlEnvironmentVariable,
            product.InstallerExecutableName,
            product.DesktopExecutableName,
            product.ServerExecutableName,
            product.CliExecutableName,
            product.TrayExecutableName);

    private static InstallerFlowProductManifest ToInstallerFlowProduct(SmokeProductManifest product)
        => new(
            product.DisplayName,
            product.ArtifactBaseName,
            product.ArtifactBaseName.Replace("-", "", StringComparison.Ordinal).ToUpperInvariant());
}
