using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Providers;
using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Services;

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
        var installer = Path.Join(request.ArtifactDirectory, $"agent-up-windows-{request.RuntimeId}.exe");
        var productMsi = Path.Join(request.ArtifactDirectory, $"agent-up-windows-{request.RuntimeId}.msi");
        assert.FileExists(installer, "windows.artifact");
        assert.FileExists(productMsi, "windows.product.msi");
        if (!File.Exists(installer) || !File.Exists(productMsi))
            return new PackageValidationResult(null, null, assert.Findings);

        var layoutDirectory = Path.Join(request.WorkDirectory, "layout");
        var layout = await _archive.CreateLayoutAsync(installer, layoutDirectory, cancellationToken);
        if (!layout.Succeeded)
        {
            assert.Error("windows.layout", layout.ErrorMessage!);
            return new PackageValidationResult(null, null, assert.Findings);
        }

        return new PackageValidationResult(null, null, assert.Findings);
    }
}
