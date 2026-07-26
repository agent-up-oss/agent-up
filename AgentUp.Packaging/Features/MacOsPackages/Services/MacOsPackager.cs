using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.MacOsPackages.Providers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.MacOsPackages.Services;

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

        new MacOsPackageStager(_writer).Stage(layout, MacOsPackageManifest.From(request));

        await _packageTool.BuildComponentPackagesAsync(request, layout, cancellationToken);
        await _packageTool.BuildProductPackageAsync(layout, cancellationToken);
    }
}
