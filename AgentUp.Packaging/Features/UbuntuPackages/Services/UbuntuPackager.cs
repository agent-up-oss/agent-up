using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Features.UbuntuPackages.Providers;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Packaging.Features.UbuntuPackages.Services;

public sealed class UbuntuPackager
{
    private readonly IPackageWriter _writer;
    private readonly PayloadStagingController _payloads;
    private readonly IUbuntuPackageTool _packageTool;
    private readonly ProductManifest _product;

    public UbuntuPackager(IPackageWriter writer, PayloadStagingController payloads, IUbuntuPackageTool packageTool, ProductManifest? product = null)
    {
        _writer = writer;
        _payloads = payloads;
        _packageTool = packageTool;
        _product = product ?? ProductManifest.AgentUp();
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = UbuntuPackageLayout.From(request, _product);
        var manifest = UbuntuPackageManifest.From(request, _product);
        await _payloads.StageAsync(new PayloadStagingRequest(
            request,
            null,
            layout.DesktopPublishDirectory,
            layout.ServerPublishDirectory,
            layout.CliPublishDirectory),
            cancellationToken);

        new UbuntuPackageStager(_writer).Stage(request, layout, manifest);
        await _packageTool.BuildDebAsync(layout, cancellationToken);
    }
}
