using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.MacOsPackages.Providers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Shared.Interfaces;

namespace AgentUp.Packaging.Features.MacOsPackages.Services;

public sealed class MacOsPackager
{
    private readonly ICommandRunner _commands;
    private readonly IMacOsPackageWriter _writer;
    private readonly PayloadStagingController _payloads;

    public MacOsPackager(ICommandRunner commands, IMacOsPackageWriter writer, PayloadStagingController payloads)
    {
        _commands = commands;
        _writer = writer;
        _payloads = payloads;
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = MacOsPackageLayout.From(request);
        await _payloads.StageAsync(new PayloadStagingRequest(
            request,
            layout.InstallerPublishDirectory,
            layout.DesktopPublishDirectory,
            layout.ServerPublishDirectory,
            layout.CliPublishDirectory),
            cancellationToken);

        new MacOsPackageStager(_writer).Stage(layout, MacOsPackageManifest.From(request));

        await _commands.RunAsync(new CommandSpec("pkgbuild",
        [
            "--identifier", "dev.agent-up.installer",
            "--version", request.NormalizedVersion,
            "--root", layout.InstallerComponentRoot,
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
        await _commands.RunAsync(new CommandSpec("productbuild",
        [
            "--distribution", layout.DistributionXmlPath,
            "--package-path", layout.ComponentPackageDirectory,
            layout.ProductPackagePath
        ]), cancellationToken);
    }
}
