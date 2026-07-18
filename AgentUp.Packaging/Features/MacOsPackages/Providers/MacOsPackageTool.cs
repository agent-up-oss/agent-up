using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Features.MacOsPackages.Providers;

public sealed class MacOsPackageTool : IMacOsPackageTool
{
    private readonly ICommandRunner _commands;

    public MacOsPackageTool(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task BuildComponentPackagesAsync(PackageRequest request, MacOsPackageLayout layout, CancellationToken cancellationToken = default)
    {
        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.installer",
            "--version", request.NormalizedVersion,
            "--root", layout.InstallerComponentRoot,
            "--scripts", layout.InstallerScriptsDirectory,
            "--install-location", "/",
            layout.InstallerPackagePath
        ]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.desktop",
            "--version", request.NormalizedVersion,
            "--root", layout.DesktopComponentRoot,
            "--install-location", "/",
            layout.DesktopPackagePath
        ]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.cli",
            "--version", request.NormalizedVersion,
            "--root", layout.CliComponentRoot,
            "--install-location", "/",
            layout.CliPackagePath
        ]), cancellationToken);
        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.server",
            "--version", request.NormalizedVersion,
            "--root", layout.ServerComponentRoot,
            "--scripts", layout.ScriptsDirectory,
            "--install-location", "/",
            layout.ServerPackagePath
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
