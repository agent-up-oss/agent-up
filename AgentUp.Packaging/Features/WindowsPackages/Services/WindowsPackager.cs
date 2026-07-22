using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Controllers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.WindowsPackages.Models;

namespace AgentUp.Packaging.Features.WindowsPackages.Services;

public sealed class WindowsPackager
{
    private readonly IWindowsPackageWriter _writer;
    private readonly PayloadStagingController _payloads;
    private readonly IWindowsPackagingTool _packagingTool;

    public WindowsPackager(IWindowsPackageWriter writer, PayloadStagingController payloads, IWindowsPackagingTool packagingTool)
    {
        _writer = writer;
        _payloads = payloads;
        _packagingTool = packagingTool;
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = WindowsPackageLayout.From(request);
        await _payloads.StageAsync(new PayloadStagingRequest(
            request,
            layout.InstallerPublishDirectory,
            layout.DesktopPublishDirectory,
            layout.ServerPublishDirectory,
            layout.CliPublishDirectory),
            cancellationToken);
        _writer.CreateDirectory(layout.InstallerSourceDirectory);

        var manifest = WindowsPackageManifest.From(request);
        var generator = new WindowsWixSourceGenerator(manifest);
        _writer.WriteText(
            Path.Join(
                layout.InstallerSourceDirectory,
                AgentUp.Installers.Features.WindowsInstallation.Models.WindowsInstallerManifest.RequireSafeCliShimFileName(manifest.InstallerManifest.CliShimName)),
            WindowsWixSourceGenerator.CliShimText());
        _writer.WriteText(layout.ProductWxsPath, generator.ProductWxs(layout));
        _writer.WriteText(layout.BundleWxsPath, generator.BundleWxs(layout));
        _writer.WriteText(layout.LicenseRtfPath, WindowsWixSourceGenerator.LicenseRtf());

        await _packagingTool.AcceptWixLicenseAsync(cancellationToken);
        await _packagingTool.BuildProductMsiAsync(layout, cancellationToken);
        await _packagingTool.BuildBundleAsync(request, layout, cancellationToken);
        _writer.CopyFile(layout.ProductMsiPath, layout.ProductMsiOutputPath);
    }
}
