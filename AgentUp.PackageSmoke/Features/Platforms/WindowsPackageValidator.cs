using AgentUp.PackageSmoke.Features.Validation;

namespace AgentUp.PackageSmoke.Features.Platforms;

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
        var installer = Path.Combine(request.ArtifactDirectory, $"agent-up-windows-{request.RuntimeId}.exe");
        assert.FileExists(installer, "windows.artifact");
        if (!File.Exists(installer))
            return new PackageValidationResult(null, null, assert.Findings);

        var layoutDirectory = Path.Combine(request.WorkDirectory, "layout");
        var layout = await _commands.RunAsync(new CommandSpec(installer, ["/layout", layoutDirectory, "/quiet"]), cancellationToken);
        if (layout.ExitCode != 0)
        {
            assert.Error("windows.layout", $"installer layout failed: {layout.Stderr}{layout.Stdout}");
            return new PackageValidationResult(null, null, assert.Findings);
        }

        assert.FileExists(Path.Combine(layoutDirectory, "Product.msi"), "windows.product.msi");

        return new PackageValidationResult(null, null, assert.Findings);
    }
}
