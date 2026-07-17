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
        const string layoutScript = "$process = Start-Process -FilePath $env:AGENTUP_SMOKE_INSTALLER -ArgumentList @('/layout', $env:AGENTUP_SMOKE_LAYOUT, '/quiet') -Wait -PassThru; exit $process.ExitCode";
        var environment = new Dictionary<string, string>
        {
            ["AGENTUP_SMOKE_INSTALLER"] = installer,
            ["AGENTUP_SMOKE_LAYOUT"] = layoutDirectory
        };

        var result = await _commands.RunAsync(new CommandSpec("powershell.exe", ["-NoProfile", "-Command", layoutScript], Environment: environment), cancellationToken);
        return result.ExitCode == 0
            ? PackageArchiveOperationResult.Success()
            : PackageArchiveOperationResult.Failure($"installer layout failed: {result.Stderr}{result.Stdout}");
    }
}
