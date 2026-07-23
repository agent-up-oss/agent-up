using AgentUp.Packaging.Features.MacOsPackages.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Controllers;
using AgentUp.Packaging.Features.WindowsPackages.Controllers;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Services;

public sealed class PackageCommandService
{
    private readonly IPackageCommandParser _parser;
    private readonly IRepositoryPathProvider _repositoryPaths;
    private readonly IEnvironmentVariableProvider _environment;
    private readonly IUbuntuPackageController _ubuntu;
    private readonly IWindowsPackageController _windows;
    private readonly IMacOsPackageController _macOs;

    public PackageCommandService(
        IPackageCommandParser parser,
        IRepositoryPathProvider repositoryPaths,
        IEnvironmentVariableProvider environment,
        IUbuntuPackageController ubuntu,
        IWindowsPackageController windows,
        IMacOsPackageController macOs)
    {
        _parser = parser;
        _repositoryPaths = repositoryPaths;
        _environment = environment;
        _ubuntu = ubuntu;
        _windows = windows;
        _macOs = macOs;
    }

    public async Task<int> ExecuteAsync(
        string[] args,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        var parsed = _parser.Parse(args);
        if (!parsed.Succeeded)
        {
            standardError.WriteLine(parsed.ErrorMessage);
            return 2;
        }

        var result = await ExecuteAsync(parsed.Command!, cancellationToken);
        if (result.ErrorMessage is not null)
            standardError.WriteLine(result.ErrorMessage);

        return result.ExitCode;
    }

    public async Task<PackageCommandResult> ExecuteAsync(PackageCommand command, CancellationToken cancellationToken = default)
    {
        PackageRequest request;
        try
        {
            request = new PackageRequest(
                _repositoryPaths.FindRepositoryRoot(),
                command.Platform,
                command.RuntimeId,
                command.Version,
                command.OutputDirectory,
                _environment.Get("CONFIGURATION") ?? "Release",
                command.PayloadRoot);
        }
        catch (ArgumentException exception)
        {
            return new PackageCommandResult(2, exception.Message);
        }

        switch (command.Platform)
        {
            case "ubuntu":
                await _ubuntu.PackageAsync(request, cancellationToken);
                return new PackageCommandResult(0);
            case "windows":
                await _windows.PackageAsync(request, cancellationToken);
                return new PackageCommandResult(0);
            case "macos":
                await _macOs.PackageAsync(request, cancellationToken);
                return new PackageCommandResult(0);
            default:
                return new PackageCommandResult(78, $"Platform '{command.Platform}' is not yet implemented by AgentUp.Packaging.");
        }
    }
}
