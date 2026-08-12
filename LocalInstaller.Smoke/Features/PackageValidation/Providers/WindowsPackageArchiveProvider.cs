using LocalInstaller.Smoke.Features.PackageValidation.DTOs;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;

namespace LocalInstaller.Smoke.Features.PackageValidation.Providers;

public sealed class WindowsPackageArchiveProvider : IWindowsPackageArchiveProvider
{
    private readonly ICommandRunner _commands;

    public WindowsPackageArchiveProvider(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<PackageArchiveOperationResult> CreateLayoutAsync(string installer, string layoutDirectory, CancellationToken cancellationToken = default)
    {
        const string layoutScript = "$process = Start-Process -FilePath $env:LOCALINSTALLER_SMOKE_INSTALLER -ArgumentList @('/layout', $env:LOCALINSTALLER_SMOKE_LAYOUT, '/quiet') -Wait -PassThru; exit $process.ExitCode";
        var environment = new Dictionary<string, string>
        {
            ["LOCALINSTALLER_SMOKE_INSTALLER"] = installer,
            ["LOCALINSTALLER_SMOKE_LAYOUT"] = layoutDirectory
        };

        var result = await _commands.RunAsync(new CommandSpec("powershell.exe", ["-NoProfile", "-Command", layoutScript], Environment: environment), cancellationToken);
        return result.ExitCode == 0
            ? PackageArchiveOperationResult.Success()
            : PackageArchiveOperationResult.Failure($"installer layout failed: {result.Stderr}{result.Stdout}");
    }
}
