using AgentUp.Packaging.Features.MacOsPackages.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Controllers;
using AgentUp.Packaging.Features.WindowsPackages.Controllers;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Services;

public sealed class PackageCommandService
{
    private const string Usage = "Usage: AgentUp.Packaging package <platform> <runtime-id> <version> [output-dir] [--payload-root <path>]";
    private readonly IRepositoryPathProvider _repositoryPaths;
    private readonly IEnvironmentVariableProvider _environment;
    private readonly IUbuntuPackageController _ubuntu;
    private readonly IWindowsPackageController _windows;
    private readonly IMacOsPackageController _macOs;

    public PackageCommandService(
        IRepositoryPathProvider repositoryPaths,
        IEnvironmentVariableProvider environment,
        IUbuntuPackageController ubuntu,
        IWindowsPackageController windows,
        IMacOsPackageController macOs)
    {
        _repositoryPaths = repositoryPaths;
        _environment = environment;
        _ubuntu = ubuntu;
        _windows = windows;
        _macOs = macOs;
    }

    public async Task<int> ExecuteAsync(string[] args, TextWriter standardError, CancellationToken cancellationToken = default)
    {
        if (!TryParse(args, standardError, out var parsed))
            return 2;

        var request = new PackageRequest(
            _repositoryPaths.FindRepositoryRoot(),
            parsed.Platform,
            parsed.RuntimeId,
            parsed.Version,
            parsed.OutputDirectory,
            _environment.Get("CONFIGURATION") ?? "Release",
            parsed.PayloadRoot);

        switch (parsed.Platform)
        {
            case "ubuntu":
                await _ubuntu.PackageAsync(request, cancellationToken);
                return 0;
            case "windows":
                await _windows.PackageAsync(request, cancellationToken);
                return 0;
            case "macos":
                await _macOs.PackageAsync(request, cancellationToken);
                return 0;
            default:
                standardError.WriteLine($"Platform '{parsed.Platform}' is not yet implemented by AgentUp.Packaging.");
                return 78;
        }
    }

    private bool TryParse(string[] args, TextWriter standardError, out ParsedPackageCommand parsed)
    {
        parsed = default;
        if (args.Length is < 4 or > 7 || args[0] != "package")
        {
            standardError.WriteLine(Usage);
            return false;
        }

        var outputDirectory = "artifacts";
        var payloadRoot = _environment.Get("AGENTUP_PACKAGE_PAYLOAD_ROOT");
        var index = 4;

        if (index < args.Length && args[index] != "--payload-root")
        {
            outputDirectory = args[index];
            index++;
        }

        if (index < args.Length)
        {
            if (index + 1 >= args.Length || args[index] != "--payload-root")
            {
                standardError.WriteLine(Usage);
                return false;
            }

            payloadRoot = args[index + 1];
            index += 2;
        }

        if (index != args.Length)
        {
            standardError.WriteLine(Usage);
            return false;
        }

        parsed = new ParsedPackageCommand(args[1], args[2], args[3], outputDirectory, payloadRoot);
        return true;
    }

    private readonly record struct ParsedPackageCommand(
        string Platform,
        string RuntimeId,
        string Version,
        string OutputDirectory,
        string? PayloadRoot);
}
