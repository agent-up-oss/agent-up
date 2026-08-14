using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Shared.Providers;

namespace LocalInstaller.Smoke.Features.PackageValidation.Services;

public sealed class WindowsPackageValidator : IPackageValidator
{
    private readonly IWindowsPackageArchiveProvider _archive;

    public WindowsPackageValidator(IWindowsPackageArchiveProvider archive)
    {
        _archive = archive;
    }

    public async Task<PackageValidationResult> ValidateAsync(PackageValidationRequest request, CancellationToken cancellationToken = default)
    {
        var assert = new FileAssertions();
        var pkg = request.Product.ArtifactBaseName;
        var installer = SafeSmokePaths.Child(request.ArtifactDirectory, $"{pkg}-windows-{request.RuntimeId}.exe");
        var productMsi = SafeSmokePaths.Child(request.ArtifactDirectory, $"{pkg}-windows-{request.RuntimeId}.msi");
        assert.FileExists(installer, "windows.artifact");
        assert.FileExists(productMsi, "windows.product.msi");
        if (!File.Exists(installer) || !File.Exists(productMsi))
            return new PackageValidationResult(null, null, assert.Findings);

        var layoutDirectory = SafeSmokePaths.Child(request.WorkDirectory, "layout");
        var layout = await _archive.CreateLayoutAsync(installer, layoutDirectory, cancellationToken);
        if (!layout.Succeeded)
        {
            assert.Error("windows.layout", layout.ErrorMessage!);
            return new PackageValidationResult(null, null, assert.Findings);
        }

        return new PackageValidationResult(null, null, assert.Findings);
    }
}
