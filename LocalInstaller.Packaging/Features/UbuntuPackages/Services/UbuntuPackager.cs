using LocalInstaller.Packaging.Features.ReleaseArtifacts.Controllers;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.UbuntuPackages.Interfaces;
using LocalInstaller.Packaging.Features.UbuntuPackages.Models;

namespace LocalInstaller.Packaging.Features.UbuntuPackages.Services;

public sealed partial class UbuntuPackager
{
    private readonly IPackageWriter _writer;
    private readonly PayloadStagingController _payloads;
    private readonly IUbuntuPackageTool _packageTool;
    private readonly PackageProductManifest _product;

    public UbuntuPackager(IPackageWriter writer, PayloadStagingController payloads, IUbuntuPackageTool packageTool, PackageProductManifest product)
    {
        _writer = writer;
        _payloads = payloads;
        _packageTool = packageTool;
        _product = product;
        PackageProductManifest.Validate(_product);
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = UbuntuPackageLayout.From(request, _product);
        var manifest = UbuntuPackageManifest.From(request, _product);
        await _payloads.StageAsync(new PayloadStagingRequest(
            request,
            layout.InstallerPublishDirectory,
            layout.DesktopPublishDirectory,
            layout.ServerPublishDirectory,
            layout.CliPublishDirectory,
            layout.TrayPublishDirectory),
            cancellationToken);

        new UbuntuPackageStager(_writer).Stage(request, layout, manifest);
        await _packageTool.BuildDebAsync(layout, cancellationToken);
    }
}
