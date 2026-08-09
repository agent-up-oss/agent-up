using LocalInstaller.Packaging.Features.MacOsPackages.Interfaces;
using LocalInstaller.Packaging.Features.MacOsPackages.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Controllers;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.MacOsPackages.Services;

public sealed class MacOsPackager
{
    private readonly IMacOsPackageWriter _writer;
    private readonly PayloadStagingController _payloads;
    private readonly IMacOsPackageTool _packageTool;

    public MacOsPackager(IMacOsPackageWriter writer, PayloadStagingController payloads, IMacOsPackageTool packageTool)
    {
        _writer = writer;
        _payloads = payloads;
        _packageTool = packageTool;
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = MacOsPackageLayout.From(request);
        await _payloads.StageAsync(new PayloadStagingRequest(
            request,
            layout.InstallerPublishDirectory,
            layout.DesktopPublishDirectory,
            layout.ServerPublishDirectory,
            layout.CliPublishDirectory,
            layout.TrayPublishDirectory),
            cancellationToken);

        var manifest = MacOsPackageManifest.From(request);
        new MacOsPackageStager(_writer).Stage(layout, manifest, request);

        await _packageTool.BuildComponentPackagesAsync(request, layout, manifest, cancellationToken);
        await _packageTool.BuildProductPackageAsync(layout, cancellationToken);
    }
}
