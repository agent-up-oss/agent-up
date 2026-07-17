using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Providers;

public sealed class MacOsPackageArchiveProvider : IMacOsPackageArchiveProvider
{
    private readonly ICommandRunner _commands;

    public MacOsPackageArchiveProvider(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<PackageArchiveOperationResult> ExpandAsync(string archive, string expandedDirectory, CancellationToken cancellationToken = default)
    {
        var result = await _commands.RunAsync(new CommandSpec("pkgutil", ["--expand-full", archive, expandedDirectory]), cancellationToken);
        return result.ExitCode == 0
            ? PackageArchiveOperationResult.Success()
            : PackageArchiveOperationResult.Failure($"pkgutil failed: {result.Stderr}{result.Stdout}");
    }

    public string FindFirst(string root, string suffix)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
               .FirstOrDefault(path => path.EndsWith(suffix, StringComparison.Ordinal)) ?? "";

    public string FindDistribution(string root)
        => Directory.EnumerateFiles(root, "Distribution", SearchOption.AllDirectories).FirstOrDefault() ?? "";
}
