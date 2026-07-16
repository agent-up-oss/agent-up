using AgentUp.PackageSmoke.Features.Validation.DTOs;
using AgentUp.PackageSmoke.Features.Validation.Providers;
using AgentUp.PackageSmoke.Features.Validation.Services;

namespace AgentUp.PackageSmoke.Features.Platforms.Services;

public sealed class WindowsPackageValidator : IPackageValidator
{
    private readonly ICommandRunner _commands;

    public WindowsPackageValidator(ICommandRunner commands)
    {
        _commands = commands;
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
        var layout = await _commands.RunAsync(new CommandSpec(installer, ["/layout", layoutDirectory, "/quiet"]), cancellationToken);
        if (layout.ExitCode != 0)
        {
            assert.Error("windows.layout", $"installer layout failed: {layout.Stderr}{layout.Stdout}");
            return new PackageValidationResult(null, null, assert.Findings);
        }

        return new PackageValidationResult(null, null, assert.Findings);
    }
}
