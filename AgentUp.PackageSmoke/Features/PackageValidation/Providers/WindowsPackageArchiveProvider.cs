using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Providers;

public sealed class WindowsPackageArchiveProvider : IWindowsPackageArchiveProvider
{
    private readonly ICommandRunner _commands;

    public WindowsPackageArchiveProvider(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<PackageArchiveOperationResult> CreateLayoutAsync(string installer, string layoutDirectory, CancellationToken cancellationToken = default)
    {
        var result = await _commands.RunAsync(new CommandSpec(installer, ["/layout", layoutDirectory, "/quiet"]), cancellationToken);
        return result.ExitCode == 0
            ? PackageArchiveOperationResult.Success()
            : PackageArchiveOperationResult.Failure($"installer layout failed: {result.Stderr}{result.Stdout}");
    }
}
