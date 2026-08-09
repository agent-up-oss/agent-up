using LocalInstaller.Packaging.Features.MacOsPackages.Interfaces;
using LocalInstaller.Packaging.Features.MacOsPackages.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Shared.Interfaces;

namespace LocalInstaller.Packaging.Features.MacOsPackages.Providers;

public sealed class MacOsPackageTool : IMacOsPackageTool
{
    private readonly ICommandRunner _commands;

    public MacOsPackageTool(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task BuildComponentPackagesAsync(PackageRequest request, MacOsPackageLayout layout, MacOsPackageManifest manifest, CancellationToken cancellationToken = default)
    {
        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", manifest.InstallerManifest.InstallerBundleIdentifier,
            "--version", request.NormalizedVersion,
            "--root", layout.InstallerComponentRoot,
            "--scripts", layout.InstallerScriptsDirectory,
            "--install-location", "/",
            layout.InstallerPackagePath
        ]), cancellationToken);
    }

    public Task BuildProductPackageAsync(MacOsPackageLayout layout, CancellationToken cancellationToken = default)
        => _commands.RunAsync(new CommandSpec("productbuild",
        [
            "--distribution", layout.DistributionXmlPath,
            "--package-path", layout.ComponentPackageDirectory,
            layout.ProductPackagePath
        ]), cancellationToken);
}
