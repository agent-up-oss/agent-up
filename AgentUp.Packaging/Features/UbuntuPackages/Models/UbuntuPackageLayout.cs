using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.UbuntuPackages.Models;

public sealed record UbuntuPackageLayout(
    string DebRoot,
    string DebOutputPath,
    string InstallerPublishDirectory,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory)
{
    public static UbuntuPackageLayout From(PackageRequest request)
        => From(request, request.ProductManifest);

    public static UbuntuPackageLayout From(PackageRequest request, PackageProductManifest product)
    {
        PackageProductManifest.Validate(product);
        var packageName = product.Slug;
        var stage = request.StageDirectory;
        return new UbuntuPackageLayout(
            DebRoot: Path.Join(stage, "deb-root"),
            DebOutputPath: Path.Join(request.OutputRoot, $"{packageName}-ubuntu-{request.RuntimeId}.deb"),
            InstallerPublishDirectory: Path.Join(stage, "installer"),
            DesktopPublishDirectory: Path.Join(stage, "desktop"),
            ServerPublishDirectory: Path.Join(stage, "server"),
            CliPublishDirectory: Path.Join(stage, "cli"));
    }
}
