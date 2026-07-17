using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;

namespace AgentUp.PackageSmoke.Features.PackageValidation.Providers;

public sealed class UbuntuPackageArchiveProvider : IUbuntuPackageArchiveProvider
{
    private readonly ICommandRunner _commands;

    public UbuntuPackageArchiveProvider(ICommandRunner commands)
    {
        _commands = commands;
    }

    public Task<PackageArchiveOperationResult> ExtractRootAsync(string archive, string rootDirectory, CancellationToken cancellationToken = default)
        => RunDpkgDebAsync(["-x", archive, rootDirectory], cancellationToken);

    public Task<PackageArchiveOperationResult> ExtractControlAsync(string archive, string controlDirectory, CancellationToken cancellationToken = default)
        => RunDpkgDebAsync(["-e", archive, controlDirectory], cancellationToken);

    private async Task<PackageArchiveOperationResult> RunDpkgDebAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync(new CommandSpec("dpkg-deb", arguments), cancellationToken);
        return result.ExitCode == 0
            ? PackageArchiveOperationResult.Success()
            : PackageArchiveOperationResult.Failure($"dpkg-deb failed: {result.Stderr}{result.Stdout}");
    }
}
